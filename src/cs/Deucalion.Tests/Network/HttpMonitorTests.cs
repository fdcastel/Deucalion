using System.Net;
using Deucalion.Network.Monitors;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Deucalion.Tests.Network;

/// <summary>
/// Hermetic: every case runs against a local server on an ephemeral port, so these assert
/// HttpMonitor's contract rather than whether the public internet happens to be reachable.
/// </summary>
public class HttpMonitorTests
{
    [Fact]
    public async Task ReturnsUp_OnSuccessStatus()
    {
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.OK, "hello");
        HttpMonitor monitor = new() { Url = server.Url };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Up, result.State);
        Assert.Null(result.ResponseText);
    }

    [Fact]
    public async Task ReturnsDown_OnFailureStatus()
    {
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.NotFound);
        HttpMonitor monitor = new() { Url = server.Url };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Not Found", result.ResponseText);
    }

    [Fact]
    public async Task ExpectedStatusCode_TurnsAFailureStatusIntoUp()
    {
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.NotFound);
        HttpMonitor monitor = new() { Url = server.Url, ExpectedStatusCode = HttpStatusCode.NotFound };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Up, result.State);
        Assert.Null(result.ResponseText);
    }

    [Fact]
    public async Task ExpectedStatusCode_TurnsASuccessStatusIntoDown()
    {
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.OK);
        HttpMonitor monitor = new() { Url = server.Url, ExpectedStatusCode = HttpStatusCode.Accepted };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
    }

    [Fact]
    public async Task ExpectedResponseBodyPattern_MatchingBodyIsUp()
    {
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.OK, """{"current_user_url":"..."}""");
        HttpMonitor monitor = new() { Url = server.Url, ExpectedResponseBodyPattern = "current_user_url" };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Up, result.State);
        Assert.Null(result.ResponseText);
    }

    [Fact]
    public async Task ExpectedResponseBodyPattern_NonMatchingBodyIsDown()
    {
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.OK, "nothing to see here");
        HttpMonitor monitor = new() { Url = server.Url, ExpectedResponseBodyPattern = "current_user_url" };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.StartsWith("Unexpected response:", result.ResponseText);
    }

    [Fact]
    public async Task ExpectedResponseBodyPattern_TruncatesLongBodiesInTheMessage()
    {
        var body = new string('x', 500);
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.OK, body);
        HttpMonitor monitor = new() { Url = server.Url, ExpectedResponseBodyPattern = "nope" };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.NotNull(result.ResponseText);
        Assert.EndsWith("...", result.ResponseText);
        Assert.True(result.ResponseText.Length < 100, $"Expected a truncated message, got {result.ResponseText.Length} chars.");
    }

    [Fact]
    public async Task InvalidResponseBodyPattern_ThrowsWhenConfigured_NotOnEveryProbe()
    {
        // Fail fast at configuration time. Previously an invalid pattern threw
        // RegexParseException from inside the probe, which killed the polling loop.
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.OK);

        Assert.ThrowsAny<ArgumentException>(() =>
            new HttpMonitor { Url = server.Url, ExpectedResponseBodyPattern = "([unclosed" });
    }

    [Fact]
    public async Task CatastrophicBacktrackingPattern_TimesOutInsteadOfHanging()
    {
        // A config-supplied pattern must not be able to wedge the monitor loop.
        await using var server = await TestHttpServer.StartAsync(HttpStatusCode.OK, new string('a', 40) + "!");
        HttpMonitor monitor = new()
        {
            Url = server.Url,
            ExpectedResponseBodyPattern = "^(a+)+$",
        };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
    }

    [Fact]
    public async Task ReturnsDown_WithTimeoutText_WhenTheServerIsTooSlow()
    {
        await using var server = await TestHttpServer.StartAsync(async context =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
        });

        HttpMonitor monitor = new() { Url = server.Url, Timeout = TimeSpan.FromMilliseconds(250) };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Timeout", result.ResponseText);
    }

    [Fact]
    public async Task ReturnsDown_WhenNothingIsListening()
    {
        // Bind then immediately release, so the port is almost certainly closed.
        var server = await TestHttpServer.StartAsync(HttpStatusCode.OK);
        var url = server.Url;
        await server.DisposeAsync();

        HttpMonitor monitor = new() { Url = url, Timeout = TimeSpan.FromSeconds(2) };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.NotNull(result.ResponseText);
    }

    [Fact]
    public async Task SlowResponse_BeyondWarnTimeout_ReportsWarn()
    {
        await using var server = await TestHttpServer.StartAsync(async context =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), context.RequestAborted);
            await context.Response.WriteAsync("ok");
        });

        HttpMonitor monitor = new()
        {
            Url = server.Url,
            Timeout = TimeSpan.FromSeconds(5),
            WarnTimeout = TimeSpan.FromMilliseconds(10),
        };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Warn, result.State);
    }
}
