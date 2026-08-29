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

    [Fact]
    public async Task Issue20_BodyPattern_DoesNotBufferTheWholeResponseBeforeMatching()
    {
        // Regression for #20: with HttpCompletionOption.ResponseContentRead the whole body was
        // buffered inside SendAsync before the 1 MB cap was applied. A server that streams past
        // the cap and then never completes the response made the monitor time out, even though
        // the pattern was already in the first kilobyte. With ResponseHeadersRead the monitor reads
        // its 1 MB, matches, and returns -- the cap is real.
        var chunk = new string('x', 64 * 1024);
        await using var server = await TestHttpServer.StartAsync(async context =>
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("needle-at-the-start ", context.RequestAborted);
            for (var i = 0; i < 32; i++) // 2 MB, well past the cap
            {
                await context.Response.WriteAsync(chunk, context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }

            // Never finish: a full-content read can only end by timeout.
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });

        HttpMonitor monitor = new()
        {
            Url = server.Url,
            ExpectedResponseBodyPattern = "needle-at-the-start",
            Timeout = TimeSpan.FromSeconds(5),
        };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Up, result.State);
    }

    [Fact]
    public async Task BodyPattern_BeyondTheOneMegabyteCap_IsNotSeen()
    {
        // Documents the cap: only the first 1 MB is inspected, so a needle after it is a miss.
        var chunk = new string('x', 64 * 1024);
        await using var server = await TestHttpServer.StartAsync(async context =>
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            for (var i = 0; i < 48; i++) // 3 MB
            {
                await context.Response.WriteAsync(chunk, context.RequestAborted);
            }
            await context.Response.WriteAsync("needle-at-the-end", context.RequestAborted);
        });

        HttpMonitor monitor = new()
        {
            Url = server.Url,
            ExpectedResponseBodyPattern = "needle-at-the-end",
            Timeout = TimeSpan.FromSeconds(5),
        };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.StartsWith("Unexpected response:", result.ResponseText);
    }

    [Fact]
    public void Issue20_CachedClients_RecyclePooledConnections()
    {
        // Regression for #20: the default handler's PooledConnectionLifetime is infinite, so a
        // daemon running for months never re-resolves DNS and keeps probing a decommissioned IP.
        foreach (var handler in HttpMonitor.CachedHandlers)
        {
            Assert.NotEqual(Timeout.InfiniteTimeSpan, handler.PooledConnectionLifetime);
            Assert.InRange(handler.PooledConnectionLifetime, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));
        }
    }

    [Fact]
    public void IgnoreCertificateErrors_HandlerAcceptsAnyCertificate()
    {
        var handler = HttpMonitor.CachedHandlers.Single(h => h.SslOptions.RemoteCertificateValidationCallback is not null);

        Assert.True(handler.SslOptions.RemoteCertificateValidationCallback!(
            this, certificate: null, chain: null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors));
    }
}
