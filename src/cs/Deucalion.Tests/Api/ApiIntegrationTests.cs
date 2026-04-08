using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using Deucalion.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Deucalion.Tests.Api;

public sealed class ApiIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _tempPath;
    private readonly string _configurationPath;
    private readonly TestApiFactory _factory;

    public ApiIntegrationTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.Api_{Guid.NewGuid()}");
        _configurationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "deucalion-sample.yaml"));

        Directory.CreateDirectory(_tempPath);

        File.WriteAllText(_configurationPath,
            """
            defaults:
              intervalWhenUp: 00:00:00.250
              intervalWhenDown: 00:00:00.250
              intervalToDown: 00:00:02

            monitors:
              web-main: !http
                url: https://example.com
                group: Main

              checkin-main: !checkin
                secret: test-secret
                group: Main
            """);

        _factory = new TestApiFactory();
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GetConfiguration_ReturnsConfiguredMetadata()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/configuration", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal("Deucalion (dev) 📡", payload.GetProperty("pageTitle").GetString());
        Assert.Equal("Deucalion. A high performance monitoring tool.", payload.GetProperty("pageDescription").GetString());
    }

    [Fact]
    public async Task GetMonitors_ReturnsConfiguredMonitors_WithStats()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var now = DateTimeOffset.UtcNow;

        await storage.SaveEventAsync("checkin-main", new StoredEvent(now, MonitorState.Up, TimeSpan.FromMilliseconds(123), null), TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/monitors", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Array, payload.ValueKind);

        var monitorNames = payload.EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("web-main", monitorNames);
        Assert.Contains("checkin-main", monitorNames);

        var checkInMonitor = payload.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "checkin-main");
        Assert.True(checkInMonitor.TryGetProperty("stats", out var stats));
        Assert.Equal((int)MonitorState.Up, stats.GetProperty("lastState").GetInt32());

        // Verify href null handling: checkin monitor has no href, so the field should be omitted (not empty string)
        Assert.False(checkInMonitor.GetProperty("config").TryGetProperty("href", out _), "Non-HTTP monitor with no href should omit 'href' rather than return empty string");

        // Verify href auto-derivation: HTTP monitor without explicit href should use URL
        var httpMonitor = payload.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "web-main");
        Assert.Equal("https://example.com", httpMonitor.GetProperty("config").GetProperty("href").GetString());
    }

    [Fact]
    public async Task CheckInEndpoint_ValidatesSecretAndReturnsExpectedStatusCodes()
    {
        using var client = _factory.CreateClient();

        using (var unknownResponse = await client.PostAsync("/api/monitors/unknown/checkin", content: null, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        }

        using (var nonCheckInResponse = await client.PostAsync("/api/monitors/web-main/checkin", content: null, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadRequest, nonCheckInResponse.StatusCode);
        }

        using (var unauthorized = await client.PostAsync("/api/monitors/checkin-main/checkin", content: null, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitors/checkin-main/checkin");
        request.Headers.Add("deucalion-checkin-secret", "test-secret");

        using var okResponse = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
    }

    [Fact]
    public async Task SseStream_BroadcastsMonitorCheckedEvent_ToConnectedClients()
    {
        using var client = _factory.CreateClient();
        var checkedEventReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        var sseTask = Task.Run(async () =>
        {
            using var response = await client.GetAsync("/api/monitors/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var parser = SseParser.Create(stream, (_, data) => Encoding.UTF8.GetString(data));

            await foreach (var item in parser.EnumerateAsync(timeout.Token))
            {
                if (item.EventType == "MonitorChecked")
                {
                    var payload = JsonSerializer.Deserialize<JsonElement>(item.Data);
                    var monitorName = payload.GetProperty("n").GetString();
                    if (monitorName == "checkin-main")
                    {
                        checkedEventReceived.TrySetResult(monitorName!);
                        return;
                    }
                }
            }
        }, timeout.Token);

        // Wait briefly to ensure SSE connection is established before triggering checkin
        await Task.Delay(200, timeout.Token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitors/checkin-main/checkin");
        request.Headers.Add("deucalion-checkin-secret", "test-secret");
        using var response2 = await client.SendAsync(request, timeout.Token);
        response2.EnsureSuccessStatusCode();

        var monitorName = await checkedEventReceived.Task.WaitAsync(timeout.Token);
        Assert.Equal("checkin-main", monitorName);

        timeout.Cancel();
        try { await sseTask; } catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        const int maxAttempts = 5;
        TryDeleteFile(_configurationPath);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(_tempPath))
                {
                    Directory.Delete(_tempPath, true);
                }

                break;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Ignore file deletion race during test cleanup.
        }
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
