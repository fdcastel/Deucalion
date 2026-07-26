using System.Net;
using System.Net.Sockets;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Network;

/// <summary>
/// Hermetic: binds a listener on an ephemeral loopback port instead of dialling 1.1.1.1:53.
/// </summary>
public class TcpMonitorTests
{
    private static TcpListener StartListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int PortOf(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    [Fact]
    public async Task ReturnsUp_WhenThePortAccepts()
    {
        var listener = StartListener();
        try
        {
            TcpMonitor monitor = new() { Host = "127.0.0.1", Port = PortOf(listener) };

            var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

            Assert.Equal(MonitorState.Up, result.State);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ReturnsDown_WhenNothingIsListening()
    {
        // Bind to claim a free port, then release it so the connect is refused.
        var listener = StartListener();
        var port = PortOf(listener);
        listener.Stop();

        TcpMonitor monitor = new() { Host = "127.0.0.1", Port = port, Timeout = TimeSpan.FromSeconds(2) };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.NotNull(result.ResponseText);
    }

    [Fact]
    public async Task SlowConnect_BeyondWarnTimeout_ReportsWarn()
    {
        var listener = StartListener();
        try
        {
            TcpMonitor monitor = new()
            {
                Host = "127.0.0.1",
                Port = PortOf(listener),
                Timeout = TimeSpan.FromSeconds(5),
                // Any real connect takes more than a single tick.
                WarnTimeout = TimeSpan.FromTicks(1),
            };

            var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

            Assert.Equal(MonitorState.Warn, result.State);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task ReturnsDown_WithTimeoutText_WhenTheConnectStalls()
    {
        // 203.0.113.0/24 is TEST-NET-3 (RFC 5737): reserved for documentation and never routed,
        // so the connect hangs rather than being refused. Needs real outbound networking, and
        // some environments answer with an immediate ICMP unreachable instead.
        TcpMonitor monitor = new() { Host = "203.0.113.1", Port = 54, Timeout = TimeSpan.FromMilliseconds(200) };

        var result = await monitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Timeout", result.ResponseText);
    }
}
