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

[Collection(ProcessEnvironmentCollection.Name)]
public sealed class ApiIntegrationTests : IAsyncLifetime, IDisposable
{
    // Configuration file path is read during WebApplicationBuilder construction,
    // before WebApplicationFactory's ConfigureWebHost callbacks land — so we
    // override it via env var, which IConfiguration picks up first.
    private const string ConfigurationFileEnvVar = "Deucalion__ConfigurationFile";

    // Without this the storage path falls back to Path.Combine(Path.GetTempPath(), "Deucalion")
    // -- a machine-global database shared with any locally running dev instance, written to by
    // every test class instance, and never cleaned up.
    private const string StoragePathEnvVar = "Deucalion__StoragePath";

    private readonly string _tempPath;
    private readonly string _configurationPath;
    private readonly TestApiFactory _factory;

    public ApiIntegrationTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"Deucalion.Tests.Api_{Guid.NewGuid()}");
        _configurationPath = Path.Combine(_tempPath, "deucalion.yaml");

        Directory.CreateDirectory(_tempPath);

        // Long intervals on purpose: the engine probes each monitor exactly once at host start
        // and never again during the test, so it cannot race assertions about stored events.
        // Check-in monitors short-circuit their delay, so the SSE test still works.
        File.WriteAllText(_configurationPath,
            """
            defaults:
              intervalWhenUp: 00:05:00
              intervalWhenDown: 00:05:00
              intervalToDown: 00:05:00

            monitors:
              web-main: !http
                url: https://example.com
                group: Main

              checkin-main: !checkin
                secret: test-secret
                group: Main

              checkin-open: !checkin
                group: Main
            """);

        Environment.SetEnvironmentVariable(ConfigurationFileEnvVar, _configurationPath);
        Environment.SetEnvironmentVariable(StoragePathEnvVar, Path.Combine(_tempPath, "storage"));

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
    }

    [Fact]
    public async Task GetMonitors_ReturnsConfiguredMonitors_WithStats()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        // Ahead of now: DateTimeOffset.UtcNow has ~15.6ms granularity on Windows, so this
        // guarantees the test's event sorts after the engine's single start-up probe.
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

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
    public async Task GetSingleMonitor_ReturnsThatMonitorOnly()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/monitors/web-main", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // An object, not the array the collection route returns.
        Assert.Equal(JsonValueKind.Object, payload.ValueKind);
        Assert.Equal("web-main", payload.GetProperty("name").GetString());
        Assert.Equal("http", payload.GetProperty("config").GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetSingleMonitor_UnknownName_Returns404ProblemDetails()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/monitors/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("/api/errors/monitor-not-found", problem.GetProperty("type").GetString());
        Assert.Contains("does-not-exist", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
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
    public async Task CheckInEndpoint_WithoutConfiguredSecret_AcceptsUnauthenticatedCheckIn()
    {
        // 'secret' is optional. A check-in monitor configured without one performs no
        // authentication -- a bare POST must be accepted.
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync("/api/monitors/checkin-open/checkin", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CheckInEndpoint_WithoutConfiguredSecret_IgnoresSuppliedSecret()
    {
        // No secret configured means no secret is checked -- an unexpected header is not an error.
        using var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitors/checkin-open/checkin");
        request.Headers.Add("deucalion-checkin-secret", "whatever");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SseStream_BroadcastsMonitorCheckedEvent_ToConnectedClients()
    {
        using var client = _factory.CreateClient();
        var checkedEventReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The endpoint writes ": connected\n\n" and flushes as soon as the subscription is
        // registered, so waiting for that byte is an exact signal -- no sleeping and guessing.
        var subscribed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var sseTask = Task.Run(async () =>
        {
            using var response = await client.GetAsync("/api/monitors/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);

            // Read the preamble byte-wise; SseParser skips comment lines, so it cannot
            // surface the ": connected" frame itself.
            var buffer = new byte[64];
            var read = await stream.ReadAsync(buffer, timeout.Token);
            Assert.Contains("connected", Encoding.UTF8.GetString(buffer, 0, read), StringComparison.Ordinal);
            subscribed.TrySetResult();

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

        await subscribed.Task.WaitAsync(timeout.Token);

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
        Environment.SetEnvironmentVariable(ConfigurationFileEnvVar, null);
        Environment.SetEnvironmentVariable(StoragePathEnvVar, null);

        TestPaths.DeleteWithRetry(_tempPath);
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
