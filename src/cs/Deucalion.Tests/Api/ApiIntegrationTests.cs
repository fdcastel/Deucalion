using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using Deucalion.Api.Services;
using Deucalion.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests.Api;

[Collection(ProcessEnvironmentCollection.Name)]
public sealed class ApiIntegrationTests : IAsyncLifetime
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
                group: Web

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

    // Cleanup lives here, not in IDisposable.Dispose(): xunit.v3 calls only DisposeAsync() on a
    // class that implements IAsyncLifetime, so the old Dispose() never ran -- the env vars
    // outlived each test and every instance leaked its directory under %TEMP%.
    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();

        Environment.SetEnvironmentVariable(ConfigurationFileEnvVar, null);
        Environment.SetEnvironmentVariable(StoragePathEnvVar, null);

        TestPaths.DeleteWithRetry(_tempPath);
    }

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
    public async Task Issue23_CheckInEndpoint_IsRateLimited_PerClient()
    {
        // Unauthenticated and reachable from anywhere: without a limit one client could hammer
        // the endpoint (and brute-force a secret) without bound.
        using var client = _factory.CreateClient();

        for (var i = 0; i < Deucalion.Api.Application.CheckInRateLimitPerMinute; i++)
        {
            using var accepted = await client.PostAsync("/api/monitors/checkin-open/checkin", content: null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        using var rejected = await client.PostAsync("/api/monitors/checkin-open/checkin", content: null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        // Other endpoints are not subject to the check-in limit.
        using var monitors = await client.GetAsync("/api/monitors", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, monitors.StatusCode);
    }

    [Fact]
    public async Task CheckInEndpoint_SecretOfDifferentLength_IsRejected()
    {
        // A prefix of the secret and a longer string must both fail (length is checked before
        // the constant-time comparison).
        using var client = _factory.CreateClient();

        foreach (var wrong in new[] { "test-secre", "test-secret-and-more", "TEST-SECRET" })
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitors/checkin-main/checkin");
            request.Headers.Add("deucalion-checkin-secret", wrong);

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Issue23_CorsPreflight_ForCheckIn_AllowsMethodAndSecretHeader()
    {
        // A cross-origin check-in carries a custom header, so the browser sends a preflight.
        // Origin-only CORS emitted no Allow-Methods / Allow-Headers and the browser blocked it.
        using var client = _factory.CreateClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/monitors/checkin-main/checkin");
        preflight.Headers.Add("Origin", "https://status.example.org");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "deucalion-checkin-secret");

        using var response = await client.SendAsync(preflight, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods")), StringComparison.Ordinal);
        Assert.Contains("deucalion-checkin-secret", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Issue23_CorsHeaders_ArePresentOnUnhandledExceptionResponses()
    {
        // UseCors ran after UseExceptionHandler, so a 500 produced by the handler had no CORS
        // headers and a browser could not even read the problem details. The factory appends a
        // throwing middleware at the tail of the pipeline (see ThrowingTailFilter).
        using var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, ThrowingTailFilter.Path);
        request.Headers.Add("Origin", "https://status.example.org");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
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

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, ThrowingTailFilter>());
        }
    }

    // --- Discovery: /api/status and /api/version (#35, #16) -----------------------------------

    [Fact]
    public async Task GetStatus_ReturnsSelfDescribingSummary()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

        // checkin-main: down, then up twice -> state "up" since the first up.
        await storage.SaveEventAsync("checkin-main", new StoredEvent(now, MonitorState.Down, null, null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-main", new StoredEvent(now.AddSeconds(1), MonitorState.Up, TimeSpan.FromMilliseconds(120), null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-main", new StoredEvent(now.AddSeconds(2), MonitorState.Up, TimeSpan.FromMilliseconds(123), null), TestContext.Current.CancellationToken);
        // web-main: down -> the summary is "degraded" (some, not all, down).
        await storage.SaveEventAsync("web-main", new StoredEvent(now.AddSeconds(2), MonitorState.Down, null, null), TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/status", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal("degraded", payload.GetProperty("status").GetString());
        Assert.True(payload.GetProperty("availability").GetDouble() is > 0 and <= 100);

        // ISO-8601 UTC, not unix seconds.
        var updatedAt = payload.GetProperty("updatedAt").GetString()!;
        Assert.EndsWith("Z", updatedAt, StringComparison.Ordinal);
        Assert.True(DateTimeOffset.TryParse(updatedAt, out _));

        var monitors = payload.GetProperty("monitors").EnumerateArray().ToDictionary(m => m.GetProperty("name").GetString()!);
        Assert.Equal(["web-main", "checkin-main", "checkin-open"], monitors.Keys); // configuration order

        var checkIn = monitors["checkin-main"];
        Assert.Equal("Main", checkIn.GetProperty("group").GetString());
        Assert.Equal("checkin", checkIn.GetProperty("type").GetString());
        Assert.Equal("up", checkIn.GetProperty("state").GetString());
        Assert.Equal(123, checkIn.GetProperty("latencyMs").GetInt32());
        Assert.True(checkIn.GetProperty("availability").GetDouble() > 0);
        // `since` is the start of the trailing "up" run: the first up, not the last, not the down.
        Assert.Equal(now.AddSeconds(1).UtcDateTime, DateTime.Parse(checkIn.GetProperty("since").GetString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal), TimeSpan.FromMilliseconds(1));

        var web = monitors["web-main"];
        Assert.Equal("http", web.GetProperty("type").GetString());
        Assert.Equal("down", web.GetProperty("state").GetString());
        Assert.False(web.TryGetProperty("latencyMs", out _), "a failed probe has no latency; the key must be omitted, not null");

        var links = payload.GetProperty("links");
        Assert.Equal("/api/status", links.GetProperty("self").GetString());
        Assert.Equal("/api/monitors", links.GetProperty("monitors").GetString());
        Assert.Equal("/api/monitors/events", links.GetProperty("events").GetString());
        Assert.Equal("/api/version", links.GetProperty("version").GetString());
        Assert.Equal("/llms.txt", links.GetProperty("docs").GetString());

        // The advertised REST links resolve (the SSE stream and llms.txt are covered elsewhere).
        foreach (var rel in new[] { "monitors", "version" })
        {
            using var linked = await client.GetAsync(links.GetProperty(rel).GetString(), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        }
    }

    [Fact]
    public async Task GetStatus_AllDown_ReportsOutage()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

        foreach (var name in new[] { "web-main", "checkin-main", "checkin-open" })
        {
            await storage.SaveEventAsync(name, new StoredEvent(now, MonitorState.Down, null, null), TestContext.Current.CancellationToken);
        }

        using var client = _factory.CreateClient();
        var payload = await client.GetFromJsonAsync<JsonElement>("/api/status", TestContext.Current.CancellationToken);

        Assert.Equal("outage", payload.GetProperty("status").GetString());
        Assert.All(payload.GetProperty("monitors").EnumerateArray(), m => Assert.Equal("down", m.GetProperty("state").GetString()));
    }

    [Fact]
    public async Task GetStatus_AllUpOrWarn_ReportsOperational()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

        await storage.SaveEventAsync("web-main", new StoredEvent(now, MonitorState.Warn, TimeSpan.FromSeconds(2), null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-main", new StoredEvent(now, MonitorState.Up, TimeSpan.FromMilliseconds(5), null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-open", new StoredEvent(now, MonitorState.Up, TimeSpan.FromMilliseconds(5), null), TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        var payload = await client.GetFromJsonAsync<JsonElement>("/api/status", TestContext.Current.CancellationToken);

        Assert.Equal("operational", payload.GetProperty("status").GetString());
        Assert.Equal("warn", payload.GetProperty("monitors")[0].GetProperty("state").GetString());
    }

    [Fact]
    public async Task GetStatus_IsReadOnly_DoesNotMutateMonitors()
    {
        // Agents poll this endpoint; it must never touch the monitor objects (#15).
        using var client = _factory.CreateClient();
        var monitors = _factory.Services.GetRequiredService<IReadOnlyDictionary<string, Deucalion.Monitors.PullMonitor>>();
        var before = monitors.Values.Select(m => (m.Name, m.AutoWarnTimeout, m.WarnTimeout)).ToArray();

        using var response = await client.GetAsync("/api/status", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var after = monitors.Values.Select(m => (m.Name, m.AutoWarnTimeout, m.WarnTimeout)).ToArray();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task GetStatus_Since_IsTheStartOfTheRun_NotTheStatsWindow()
    {
        // Regression: `since` used to be derived from the last 60 events, so a monitor down for
        // longer than the stats window reported "down since the 60th-newest probe".
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var start = DateTimeOffset.UtcNow.AddMinutes(-200);

        await storage.SaveEventAsync("web-main", new StoredEvent(start, MonitorState.Up, TimeSpan.FromMilliseconds(30), null), TestContext.Current.CancellationToken);
        for (var i = 1; i <= 100; i++)
        {
            await storage.SaveEventAsync("web-main", new StoredEvent(start.AddMinutes(i), MonitorState.Down, null, null), TestContext.Current.CancellationToken);
        }

        using var client = _factory.CreateClient();
        var payload = await client.GetFromJsonAsync<JsonElement>("/api/status", TestContext.Current.CancellationToken);
        var web = payload.GetProperty("monitors").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "web-main");

        Assert.Equal("down", web.GetProperty("state").GetString());
        Assert.Equal(start.AddMinutes(1).UtcDateTime, DateTime.Parse(web.GetProperty("since").GetString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal), TimeSpan.FromMilliseconds(1));
        Assert.False(web.GetProperty("sinceIsLowerBound").GetBoolean(), "an Up probe precedes the run, so `since` is exact");
    }

    [Fact]
    public async Task GetStatus_SinceIsLowerBound_WhenTheRunReachesTheOldestEvent()
    {
        // The engine probes every monitor once at host start; a check-in monitor that nobody has
        // checked in to is Down from that first probe. Extending that Down run means no event of
        // the other kind exists at all, so `since` can only be a lower bound.
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

        await storage.SaveEventAsync("checkin-open", new StoredEvent(now, MonitorState.Down, null, null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-open", new StoredEvent(now.AddSeconds(1), MonitorState.Down, null, null), TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        var payload = await client.GetFromJsonAsync<JsonElement>("/api/status/checkin-open", TestContext.Current.CancellationToken);
        var monitor = payload.GetProperty("monitor");

        Assert.Equal("down", monitor.GetProperty("state").GetString());
        var since = DateTime.Parse(monitor.GetProperty("since").GetString()!, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.True(since <= now.UtcDateTime, "the run starts at the engine's startup probe, before the seeded events");
        Assert.True(monitor.GetProperty("sinceIsLowerBound").GetBoolean());
        Assert.False(monitor.TryGetProperty("latencyMs", out _), "a failed probe has no latency; the key must be omitted");
    }

    [Fact]
    public async Task GetStatus_GroupFilter_RestrictsMonitorsAndRecomputesTheSummary()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var now = DateTimeOffset.UtcNow.AddSeconds(1);

        // Web is down, Main is up: the whole page is "degraded", the Main group is "operational".
        await storage.SaveEventAsync("web-main", new StoredEvent(now, MonitorState.Down, null, null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-main", new StoredEvent(now, MonitorState.Up, TimeSpan.FromMilliseconds(5), null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-open", new StoredEvent(now, MonitorState.Up, TimeSpan.FromMilliseconds(5), null), TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        var whole = await client.GetFromJsonAsync<JsonElement>("/api/status", TestContext.Current.CancellationToken);
        Assert.Equal("degraded", whole.GetProperty("status").GetString());
        Assert.False(whole.TryGetProperty("group", out _));

        // Case-insensitive match; the response echoes the filter and links to itself.
        var main = await client.GetFromJsonAsync<JsonElement>("/api/status?group=main", TestContext.Current.CancellationToken);
        Assert.Equal("operational", main.GetProperty("status").GetString());
        Assert.Equal("main", main.GetProperty("group").GetString());
        Assert.Equal(["checkin-main", "checkin-open"], main.GetProperty("monitors").EnumerateArray().Select(m => m.GetProperty("name").GetString()));
        // Recomputed over the group only: the Web monitor (0 % after its Down) no longer drags it.
        Assert.True(main.GetProperty("availability").GetDouble() > whole.GetProperty("availability").GetDouble());
        Assert.Equal("/api/status?group=main", main.GetProperty("links").GetProperty("self").GetString());
        Assert.Equal("/api/status/{name}", main.GetProperty("links").GetProperty("monitorStatus").GetString());

        var web = await client.GetFromJsonAsync<JsonElement>("/api/status?group=Web", TestContext.Current.CancellationToken);
        Assert.Equal("outage", web.GetProperty("status").GetString());
        Assert.Single(web.GetProperty("monitors").EnumerateArray());
    }

    [Fact]
    public async Task GetStatus_UnknownGroup_Returns404ProblemListingTheGroups()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/status?group=nope", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("/api/errors/group-not-found", problem.GetProperty("type").GetString());
        var detail = problem.GetProperty("detail").GetString()!;
        Assert.Contains("'nope'", detail, StringComparison.Ordinal);
        Assert.Contains("'Web'", detail, StringComparison.Ordinal);
        Assert.Contains("'Main'", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMonitorStatus_MirrorsTheEntryInTheFullDocument()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var now = DateTimeOffset.UtcNow.AddSeconds(1);
        await storage.SaveEventAsync("checkin-main", new StoredEvent(now, MonitorState.Down, null, null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-main", new StoredEvent(now.AddSeconds(1), MonitorState.Up, TimeSpan.FromMilliseconds(42), null), TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        var whole = await client.GetFromJsonAsync<JsonElement>("/api/status", TestContext.Current.CancellationToken);
        var entry = whole.GetProperty("monitors").EnumerateArray().Single(m => m.GetProperty("name").GetString() == "checkin-main");

        using var response = await client.GetAsync("/api/status/checkin-main", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var single = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(entry.GetRawText(), single.GetProperty("monitor").GetRawText());
        Assert.True(DateTimeOffset.TryParse(single.GetProperty("updatedAt").GetString(), out _));
        var links = single.GetProperty("links");
        Assert.Equal("/api/status/checkin-main", links.GetProperty("self").GetString());
        Assert.Equal("/api/status", links.GetProperty("status").GetString());
        Assert.Equal("/api/monitors/checkin-main", links.GetProperty("monitor").GetString());
        Assert.Equal("/api/monitors/events", links.GetProperty("events").GetString());
    }

    [Fact]
    public async Task GetMonitorStatus_UnknownMonitor_Returns404Problem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/status/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("/api/errors/monitor-not-found", problem.GetProperty("type").GetString());
    }

    // --- GET /api/monitors: columnar events and the ?events= cap --------------------------------

    [Fact]
    public async Task GetMonitors_ShipsEventsColumnar_NewestFirst()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var t0 = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        // Older than anything the engine's startup probe stored, so the tail is deterministic.
        await storage.SaveEventAsync("checkin-open", new StoredEvent(t0, MonitorState.Down, null, null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-open", new StoredEvent(t0.AddSeconds(60), MonitorState.Warn, TimeSpan.FromMilliseconds(900), null), TestContext.Current.CancellationToken);
        await storage.SaveEventAsync("checkin-open", new StoredEvent(t0.AddSeconds(121), MonitorState.Up, TimeSpan.FromMilliseconds(118), null), TestContext.Current.CancellationToken);

        using var client = _factory.CreateClient();
        var payload = await client.GetFromJsonAsync<JsonElement>("/api/monitors/checkin-open?events=3", TestContext.Current.CancellationToken);
        var events = payload.GetProperty("events");

        Assert.Equal(1_800_000_121, events.GetProperty("at").GetInt64());
        Assert.Equal([61, 60], events.GetProperty("dt").EnumerateArray().Select(d => d.GetInt32()));
        Assert.Equal("231", events.GetProperty("st").GetString()); // Up, Warn, Down
        var ms = events.GetProperty("ms").EnumerateArray().ToArray();
        Assert.Equal(118, ms[0].GetInt32());
        Assert.Equal(900, ms[1].GetInt32());
        Assert.Equal(JsonValueKind.Null, ms[2].ValueKind);
    }

    [Theory]
    [InlineData("?events=2", 2)]
    [InlineData("?events=0", 1)]        // clamped up
    [InlineData("?events=999", 120)]    // clamped to EventHistoryCount
    [InlineData("", 120)]               // default
    public async Task GetMonitors_EventsQuery_CapsTheHistory(string query, int expectedCount)
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var t0 = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        for (var i = 0; i < 150; i++)
        {
            await storage.SaveEventAsync("checkin-main", new StoredEvent(t0.AddSeconds(i), MonitorState.Up, TimeSpan.FromMilliseconds(i), null), TestContext.Current.CancellationToken);
        }

        using var client = _factory.CreateClient();
        var payload = await client.GetFromJsonAsync<JsonElement>("/api/monitors/checkin-main" + query, TestContext.Current.CancellationToken);
        var events = payload.GetProperty("events");

        // The engine's own startup probe adds one more row than the 150 seeded here, so the
        // count is what was asked for, never more; the default and the over-cap ask both stop at 120.
        Assert.Equal(expectedCount, events.GetProperty("st").GetString()!.Length);
        Assert.Equal(expectedCount, events.GetProperty("ms").GetArrayLength());
        Assert.Equal(expectedCount - 1, events.GetProperty("dt").GetArrayLength());
    }

    [Fact]
    public async Task GetMonitors_MonitorWithoutEvents_OmitsTheEventsKey()
    {
        // The engine probes every monitor at host start, so this is asserted at the DTO level.
        Assert.Null(Deucalion.Api.Models.MonitorEventsDto.From([]));
    }

    [Fact]
    public async Task GetVersion_IdentifiesTheRunningBuild()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/version", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal("Deucalion", payload.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("version").GetString()));
        Assert.StartsWith(".NET ", payload.GetProperty("runtime").GetString(), StringComparison.Ordinal);

        var startedAt = DateTimeOffset.Parse(payload.GetProperty("startedAt").GetString()!);
        Assert.InRange(startedAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow);
    }

    [Fact]
    public void JsonContext_DeclaresProblemDetails_ForAotProblemResponses()
    {
        // #16: the deployed (native AOT) instance returned an empty 500 for unknown monitor names
        // because Results.Problem(...) could not serialize ProblemDetails -- no reflection resolver
        // exists under AOT and the source-generated context did not declare the type. This JIT
        // host cannot reproduce the failure, so pin the declaration directly.
        Assert.NotNull(Deucalion.Api.DeucalionJsonContext.Default.GetTypeInfo(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)));
    }

    /// <summary>
    /// Appends a middleware after the application's own pipeline (so after routing: only
    /// unmatched paths reach it) that throws for one path. It stands in for any unhandled
    /// exception so the exception handler's response can be inspected end to end.
    /// </summary>
    private sealed class ThrowingTailFilter : IStartupFilter
    {
        public const string Path = "/throw-for-test";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((context, nextMiddleware) =>
                context.Request.Path == Path
                    ? throw new InvalidOperationException("Simulated unhandled exception.")
                    : nextMiddleware(context));
        };
    }
}
