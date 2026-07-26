using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class HttpMonitor : PullMonitor
{
    // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines#recommended-use
    private static readonly HttpClient CachedHttpClient = new();
    private static readonly HttpClient CachedHttpClientIgnoreCertificate = new(new HttpClientHandler()
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    private const int MaxResponseBodySize = 1024 * 1024; // 1 MB

    // A config-supplied pattern is effectively untrusted input: without a bound, catastrophic
    // backtracking would wedge this monitor's polling loop indefinitely.
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);

    public required Uri Url { get; set; }

    public HttpStatusCode? ExpectedStatusCode { get; set; }

    private string? _expectedResponseBodyPattern;
    private Regex? _bodyPattern;

    /// <summary>
    /// Compiled once on assignment rather than per probe, so an invalid pattern fails while
    /// building monitors from configuration (reported as a ConfigurationErrorException) instead
    /// of throwing from inside the first probe.
    /// </summary>
    public string? ExpectedResponseBodyPattern
    {
        get => _expectedResponseBodyPattern;
        set
        {
            _bodyPattern = value is null ? null : new Regex(value, RegexOptions.None, RegexMatchTimeout);
            _expectedResponseBodyPattern = value;
        }
    }

    public bool? IgnoreCertificateErrors { get; set; }
    public HttpMethod? Method { get; set; }

    public override async Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default)
    {
        var method = Method ?? HttpMethod.Get;
        using var request = new HttpRequestMessage(method, Url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Deucalion", "1.0"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Use HttpCompletionOption.ResponseContentRead if we expect a response body.
            // Otherwise use ResponseHeadersRead to avoid reading the body unnecessarily.
            // https://www.stevejgordon.co.uk/using-httpcompletionoption-responseheadersread-to-improve-httpclient-performance-dotnet
            var completionOption = ExpectedResponseBodyPattern is not null
                ? HttpCompletionOption.ResponseContentRead
                : HttpCompletionOption.ResponseHeadersRead;

            // Use the appropriate HttpClient based on whether we ignore certificate errors or not.
            var httpClient = IgnoreCertificateErrors ?? false
                ? CachedHttpClientIgnoreCertificate
                : CachedHttpClient;

            using var response = await httpClient.SendAsync(request, completionOption, timeoutCts.Token);

            // Freezes stopwatch.Elapsed
            stopwatch.Stop();

            if (ExpectedStatusCode is not null)
            {
                if (response.StatusCode != ExpectedStatusCode)
                {
                    return MonitorResponse.Down(stopwatch.Elapsed, response.ReasonPhrase ?? response.StatusCode.ToString());
                }
            }
            else if (!response.IsSuccessStatusCode)
            {
                return MonitorResponse.Down(stopwatch.Elapsed, response.ReasonPhrase ?? response.StatusCode.ToString());
            }

            if (_bodyPattern is not null)
            {
                using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                // Rented, not allocated: a 1 MB char[] is 2 MB on the large object heap, and this
                // runs on every probe of every monitor that has a body pattern.
                var buffer = ArrayPool<char>.Shared.Rent(MaxResponseBodySize);
                string responseBody;
                try
                {
                    var charsRead = await reader.ReadBlockAsync(buffer.AsMemory(0, MaxResponseBodySize), timeoutCts.Token);
                    responseBody = new string(buffer, 0, charsRead);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }

                bool matched;
                try
                {
                    matched = _bodyPattern.IsMatch(responseBody);
                }
                catch (RegexMatchTimeoutException)
                {
                    return MonitorResponse.Down(stopwatch.Elapsed, "Response body pattern timed out");
                }

                if (!matched)
                {
                    var truncatedBody = responseBody.Length <= 60
                        ? responseBody
                        : string.Concat(responseBody.AsSpan(0, 60), "...");

                    return MonitorResponse.Down(stopwatch.Elapsed, $"Unexpected response: {truncatedBody}");
                }
            }

            return MonitorResponse.Up(stopwatch.Elapsed, warnElapsed: EffectiveWarnTimeout);
        }
        catch (HttpRequestException e)
        {
            return MonitorResponse.Down(stopwatch.Elapsed, e.Message);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            // Catch only if the cancellation was due to the timeout -- https://stackoverflow.com/a/67203842
            return MonitorResponse.Down(stopwatch.Elapsed, "Timeout");
        }
    }
}
