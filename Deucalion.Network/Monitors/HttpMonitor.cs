using System.ComponentModel.DataAnnotations;
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
        try
        {
            HttpRequestMessage request = new(HttpMethod.Get, Url);
            ProductInfoHeaderValue userAgentValue = new("Deucalion", "1.0");
            request.Headers.UserAgent.Add(userAgentValue);

            var httpClient = IgnoreCertificateErrors ?? false
                ? CachedHttpClientIgnoreCertificate
                : CachedHttpClient;

            using CancellationTokenSource cts = new(Timeout ?? DefaultHttpTimeout);
            using var response = await httpClient.SendAsync(request, cts.Token);

            if (ExpectedStatusCode is not null)
            {
                if (response.StatusCode != ExpectedStatusCode)
                {
                    return MonitorResponse.DefaultDown;
                }
            }
            else
            {
                if (!response.IsSuccessStatusCode)
                {
                    return MonitorResponse.DefaultDown;
                }
            }

            if (ExpectedResponseBodyPattern is not null)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!Regex.IsMatch(responseBody, ExpectedResponseBodyPattern))
                {
                    return MonitorResponse.DefaultDown;
                };
            }

            return MonitorResponse.DefaultUp;
        }
        catch (HttpRequestException)
        {
            return MonitorResponse.DefaultDown;
        }
        catch (OperationCanceledException)
        {
            return MonitorResponse.DefaultDown;
        }
    }
}
