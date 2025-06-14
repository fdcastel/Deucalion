using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class HttpMonitor : PullMonitor
{
    // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines#recommended-use
    private static HttpClient? _httpClient;
    private static HttpClient? _httpClientIgnoreCertificate;

    private static HttpClient CachedHttpClient => _httpClient ??= new HttpClient();
    private static HttpClient CachedHttpClientIgnoreCertificate => _httpClientIgnoreCertificate ??= new(new HttpClientHandler()
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    public required Uri Url { get; set; }

    public HttpStatusCode? ExpectedStatusCode { get; set; }
    public string? ExpectedResponseBodyPattern { get; set; }
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

            var statusCode = ExpectedStatusCode ?? HttpStatusCode.OK;
            if (response.StatusCode != statusCode)
            {
                return MonitorResponse.Down(stopwatch.Elapsed, response.ReasonPhrase ?? response.StatusCode.ToString());
            }

            if (ExpectedResponseBodyPattern is not null)
            {
                var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                if (!Regex.IsMatch(responseBody, ExpectedResponseBodyPattern))
                {
                    var truncatedBody = responseBody.Length <= 60
                        ? responseBody
                        : string.Concat(responseBody.AsSpan(0, 60), "...");

                    return MonitorResponse.Down(stopwatch.Elapsed, $"Unexpected response: {truncatedBody}");
                }
            }

            return MonitorResponse.Up(stopwatch.Elapsed, warnElapsed: WarnTimeout);
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
