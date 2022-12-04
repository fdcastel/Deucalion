using Deucalion.Monitors.Options;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Deucalion.Monitors
{
    public class HttpMonitor : IMonitor
    {
        private static readonly int DefaultTimeout = 1000;

        private static readonly HttpClient? _httpClient = null;
        private static readonly HttpClient? _httpClientIgnoreCertificate = null;

        private static HttpClient CachedHttpClient => _httpClient ?? new();
        private static HttpClient CachedHttpClientIgnoreCertificate => _httpClientIgnoreCertificate ?? new(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        public required HttpMonitorOptions Options { get; init; }

        public async Task<bool> IsUpAsync()
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, Options.Url);
                ProductInfoHeaderValue userAgentValue = new("Deucalion", "1.0");
                request.Headers.UserAgent.Add(userAgentValue);

                HttpClient httpClient = Options.IgnoreCertificateErrors ?? false
                    ? CachedHttpClientIgnoreCertificate
                    : CachedHttpClient;

                using CancellationTokenSource cts = new(Options.Timeout ?? DefaultTimeout);
                using HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token);

                if (Options.ExpectedStatusCode is not null)
                {
                    if (response.StatusCode != Options.ExpectedStatusCode)
                    {
                        return false;
                    }
                }
                else
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }
                }

                if (Options.ExpectedResponseBodyPattern is not null)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    if (!Regex.IsMatch(responseBody, Options.ExpectedResponseBodyPattern))
                    {
                        return false;
                    };
                }

                return true;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
