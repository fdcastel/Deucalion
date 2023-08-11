using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class HttpMonitor : PullMonitor
{
    private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(1);

    private static HttpClient? _httpClient;
    private static HttpClient? _httpClientIgnoreCertificate;

    private static HttpClient CachedHttpClient => _httpClient ??= new HttpClient();
    private static HttpClient CachedHttpClientIgnoreCertificate => _httpClientIgnoreCertificate ??= new(new HttpClientHandler()
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

    [Required]
    public Uri Url { get; set; } = default!;

    public HttpStatusCode? ExpectedStatusCode { get; set; }
    public string? ExpectedResponseBodyPattern { get; set; }
    public bool? IgnoreCertificateErrors { get; set; }

    public override async Task<MonitorResponse> QueryAsync()
    {
        HttpRequestMessage request = new(HttpMethod.Get, Url);
        ProductInfoHeaderValue userAgentValue = new("Deucalion", "1.0");
        request.Headers.UserAgent.Add(userAgentValue);

        var httpClient = IgnoreCertificateErrors ?? false
            ? CachedHttpClientIgnoreCertificate
            : CachedHttpClient;

        var timeout = Timeout ?? DefaultHttpTimeout;

        using CancellationTokenSource cts = new(timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await httpClient.SendAsync(request, cts.Token);
            stopwatch.Stop();

            var success = response.IsSuccessStatusCode;
            if (ExpectedStatusCode is not null)
            {
                if (ExpectedStatusCode != response.StatusCode)
                {
                    return MonitorResponse.Down(stopwatch.Elapsed, response.ReasonPhrase);
                }
            }
            else
            {
                if (!response.IsSuccessStatusCode)
                {
                    return MonitorResponse.Down(stopwatch.Elapsed, response.ReasonPhrase);
                }
            }

            if (ExpectedResponseBodyPattern is not null)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!Regex.IsMatch(responseBody, ExpectedResponseBodyPattern))
                {
                    var truncatedBody = responseBody.Length <= 60
                        ? responseBody
                        : string.Concat(responseBody.AsSpan(0, 60), "...");

                    return MonitorResponse.Down(stopwatch.Elapsed, $"Unexpected response: {truncatedBody}");
                };
            }

            return MonitorResponse.Up(elapsed: stopwatch.Elapsed, warnElapsed: WarnTimeout);
        }
        catch (HttpRequestException e)
        {
            return MonitorResponse.Down(stopwatch.Elapsed, e.Message);
        }
        catch (OperationCanceledException)
        {
            return MonitorResponse.Down(timeout, "Timeout");
        }
    }
}
