using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Network;

[Trait("Category", "Integration")]
public class TcpMonitorTests
{
    [Fact]
    public async Task TcpMonitor_ReturnsUp_WhenReachable()
    {
        TcpMonitor tcpMonitor = new() { Host = "1.1.1.1", Port = 53 };
        var result = await tcpMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, result.State);
    }

    [Fact]
    public async Task TcpMonitor_ReturnsDown_WhenUnreachable()
    {
        TcpMonitor tcpMonitor = new() { Host = "1.1.1.1", Port = 54, Timeout = TimeSpan.FromMilliseconds(200) };
        var result = await tcpMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Timeout", result.ResponseText);
    }
}
