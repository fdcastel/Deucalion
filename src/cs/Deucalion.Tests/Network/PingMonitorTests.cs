using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Network;

public class PingMonitorTests
{
    [Fact]
    public async Task PingMonitor_ReturnsUp_WhenReachable()
    {
        PingMonitor pingMonitor = new() { Host = "1.1.1.1" };
        var result = await pingMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, result.State);
        Assert.Null(result.ResponseText);
    }

    [Fact]
    public async Task PingMonitor_ReturnsDown_WhenUnreachable()
    {
        PingMonitor pingMonitor = new() { Host = "8.8.8.7", Timeout = TimeSpan.FromMilliseconds(200) };
        var result = await pingMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, result.State);
        Assert.NotNull(result.ResponseText);
    }
}
