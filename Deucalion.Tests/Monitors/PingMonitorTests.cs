using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class PingMonitorTests
    {
        [Fact]
        public async Task PingMonitor_ReturnsUp_WhenReachable()
        {
            PingMonitor pingMonitor = new() { Options = new() { Host = "192.168.10.1" } };
            var result = await pingMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result.State);
        }

        [Fact]
        public async Task PingMonitor_ReturnsDown_WhenUnreachable()
        {
            PingMonitor pingMonitor = new() { Options = new() { Host = "192.168.1.1", Timeout = TimeSpan.FromMilliseconds(200) } };
            var result = await pingMonitor.QueryAsync();
            Assert.Equal(MonitorState.Down, result.State);
        }
    }
}
