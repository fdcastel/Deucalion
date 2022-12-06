using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public class HttpMonitor : IPullMonitor<HttpMonitorOptions>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        private static readonly HttpClient? _httpClient;
        private static readonly HttpClient? _httpClientIgnoreCertificate;

        private static HttpClient CachedHttpClient => _httpClient ?? new();
        private static HttpClient CachedHttpClientIgnoreCertificate => _httpClientIgnoreCertificate ?? new(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        public required HttpMonitorOptions Options { get; init; }

        public async Task<MonitorState> QueryAsync()
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, Options.Url);
                ProductInfoHeaderValue userAgentValue = new("Deucalion", "1.0");
                request.Headers.UserAgent.Add(userAgentValue);

                var httpClient = Options.IgnoreCertificateErrors ?? false
                    ? CachedHttpClientIgnoreCertificate
                    : CachedHttpClient;

                using CancellationTokenSource cts = new(Options.Timeout ?? DefaultTimeout);
                using var response = await httpClient.SendAsync(request, cts.Token);

                if (Options.ExpectedStatusCode is not null)
                {
                    if (response.StatusCode != Options.ExpectedStatusCode)
                    {
                        return MonitorState.Down;
                    }
                }
                else
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return MonitorState.Down;
                    }
                }

                if (Options.ExpectedResponseBodyPattern is not null)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    if (!Regex.IsMatch(responseBody, Options.ExpectedResponseBodyPattern))
                    {
                        return MonitorState.Down;
                    };
                }

                return MonitorState.Up;
            }
            catch (HttpRequestException)
            {
                return MonitorState.Down;
            }
            catch (OperationCanceledException)
            {
                return MonitorState.Down;
            }
        }
    }
}
