﻿using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Deucalion.Monitors;
using Deucalion.Network.Monitors.Options;

namespace Deucalion.Network.Monitors
{
    public class HttpMonitor : IPullMonitor<HttpMonitorOptions>
    {
        private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(1);

        private static HttpClient? _httpClient;
        private static HttpClient? _httpClientIgnoreCertificate;

        private static HttpClient CachedHttpClient => _httpClient ??= new HttpClient();
        private static HttpClient CachedHttpClientIgnoreCertificate => _httpClientIgnoreCertificate ??= new(new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        public required HttpMonitorOptions Options { get; init; }

        public async Task<MonitorResponse> QueryAsync()
        {
            try
            {
                HttpRequestMessage request = new(HttpMethod.Get, Options.Url);
                ProductInfoHeaderValue userAgentValue = new("Deucalion", "1.0");
                request.Headers.UserAgent.Add(userAgentValue);

                var httpClient = Options.IgnoreCertificateErrors ?? false
                    ? CachedHttpClientIgnoreCertificate
                    : CachedHttpClient;

                using CancellationTokenSource cts = new(Options.Timeout ?? DefaultHttpTimeout);
                using var response = await httpClient.SendAsync(request, cts.Token);

                if (Options.ExpectedStatusCode is not null)
                {
                    if (response.StatusCode != Options.ExpectedStatusCode)
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

                if (Options.ExpectedResponseBodyPattern is not null)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    if (!Regex.IsMatch(responseBody, Options.ExpectedResponseBodyPattern))
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
}
