using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using Deucalion.Api.Services;
using Deucalion.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
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

        // The endpoint writes its preamble and flushes as soon as the subscription is
        // registered, so waiting for that block is an exact signal -- no sleeping and guessing.
        var preambleReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var sseTask = Task.Run(async () =>
        {
            using var response = await client.GetAsync("/api/monitors/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("no", Assert.Single(response.Headers.GetValues("X-Accel-Buffering")));

            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);

            // Read the preamble byte-wise; SseParser skips comment lines, so it cannot
            // surface the ": connected" frame itself.
            preambleReceived.TrySetResult(await ReadFrameAsync(stream, timeout.Token));

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

        var preamble = await preambleReceived.Task.WaitAsync(timeout.Token);
        Assert.Contains(": connected", preamble, StringComparison.Ordinal);
        // #18: the reconnect delay is pinned instead of left to the browser default.
        Assert.Contains("retry: 3000", preamble, StringComparison.Ordinal);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitors/checkin-main/checkin");
        request.Headers.Add("deucalion-checkin-secret", "test-secret");
        using var response2 = await client.SendAsync(request, timeout.Token);
        response2.EnsureSuccessStatusCode();

        var monitorName = await checkedEventReceived.Task.WaitAsync(timeout.Token);
        Assert.Equal("checkin-main", monitorName);

        timeout.Cancel();
        try { await sseTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task SseStream_ClientAbort_UnsubscribesWithoutAnErrorLog()
    {
        // #18: aborting the request must bring the subscriber count back to zero, and the
        // OperationCanceledException that ends the read loop must not be logged as an error.
        var logs = new CapturingLoggerProvider();
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();

        var broadcaster = factory.Services.GetRequiredService<MonitorEventBroadcaster>();
        var unsubscribed = WhenNoSubscribers(broadcaster);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var response = await client.GetAsync("/api/monitors/events", HttpCompletionOption.ResponseHeadersRead, requestCts.Token);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(requestCts.Token);
        await ReadFrameAsync(stream, requestCts.Token);

        Assert.Equal(1, broadcaster.SubscriberCount);

        // Abort the request the way a browser closing the tab would.
        requestCts.Cancel();
        response.Dispose();

        await unsubscribed.WaitAsync(timeout.Token);
        Assert.Equal(0, broadcaster.SubscriberCount);

        var errors = logs.Snapshot().Where(e => e.Level >= LogLevel.Error).ToList();
        Assert.True(errors.Count == 0, "Unexpected error logs:\n" + string.Join("\n", errors.Select(e => $"{e.Category}: {e.Message}")));
    }

    [Fact]
    public async Task SseStream_WriteFailureAfterClientWentAway_Unsubscribes()
    {
        // #18: unsubscribe must happen however the read loop exits. Here the client drops the
        // response without cancelling anything and the endpoint only finds out on its next
        // write. (TestServer still surfaces this through RequestAborted; the endpoint's
        // finally block is what covers transports that do not.)
        using var client = _factory.CreateClient();

        var broadcaster = _factory.Services.GetRequiredService<MonitorEventBroadcaster>();
        var unsubscribed = WhenNoSubscribers(broadcaster);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var response = await client.GetAsync("/api/monitors/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        await ReadFrameAsync(stream, timeout.Token);

        Assert.Equal(1, broadcaster.SubscriberCount);

        // Drop the response without cancelling anything, then force a write.
        stream.Dispose();
        response.Dispose();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitors/checkin-main/checkin");
        request.Headers.Add("deucalion-checkin-secret", "test-secret");
        using var checkIn = await client.SendAsync(request, timeout.Token);
        checkIn.EnsureSuccessStatusCode();

        await unsubscribed.WaitAsync(timeout.Token);
        Assert.Equal(0, broadcaster.SubscriberCount);
    }

    [Fact]
    public async Task SseStream_SendsKeepAliveWhenIdle()
    {
        // Regression for #18: an idle stream carried no bytes after the preamble, so a proxy
        // could drop it silently. The keep-alive is driven by the injected clock: without it
        // this test never sees a comment frame and times out.
        var time = new FakeTimeProvider();
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(time)));
        using var client = factory.CreateClient();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        using var response = await client.GetAsync("/api/monitors/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        await ReadFrameAsync(stream, timeout.Token);

        // The engine's start-up probes may interleave MonitorChecked frames; skip those.
        for (var i = 0; i < 2; i++)
        {
            time.Advance(MonitorEventBroadcaster.KeepAliveInterval);

            string frame;
            do
            {
                frame = await ReadFrameAsync(stream, timeout.Token);
            } while (frame.StartsWith("event:", StringComparison.Ordinal));

            Assert.Equal(MonitorEventBroadcaster.KeepAliveFrame, frame);
        }
    }

    private static Task WhenNoSubscribers(MonitorEventBroadcaster broadcaster)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        broadcaster.SubscriptionsChanged += () =>
        {
            if (broadcaster.SubscriberCount == 0)
                tcs.TrySetResult();
        };
        return tcs.Task;
    }

    /// <summary>Reads one <c>text/event-stream</c> block, i.e. up to and including the blank line.</summary>
    private static Task<string> ReadFrameAsync(Stream stream, CancellationToken cancellationToken) =>
        ReadUntilAsync(stream, "\n\n", cancellationToken);

    /// <summary>Reads until the accumulated text ends with <paramref name="terminator"/>.</summary>
    private static async Task<string> ReadUntilAsync(Stream stream, string terminator, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new byte[1];
        while (!text.ToString().EndsWith(terminator, StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                throw new EndOfStreamException($"Stream ended before the terminator. Got: '{text}'");
            text.Append((char)buffer[0]);
        }
        return text.ToString();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<(LogLevel Level, string Category, string Message)> _entries = [];

        /// <summary>Copy taken under the lock: the host keeps logging while the test reads.</summary>
        public IReadOnlyList<(LogLevel Level, string Category, string Message)> Snapshot()
        {
            lock (_entries)
                return [.. _entries];
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

        public void Dispose() { }

        private sealed class CapturingLogger(string category, List<(LogLevel, string, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (entries)
                    entries.Add((logLevel, category, formatter(state, exception) + (exception is null ? "" : $" [{exception.GetType().Name}]")));
            }
        }
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
