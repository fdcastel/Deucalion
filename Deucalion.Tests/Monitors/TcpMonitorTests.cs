using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class TcpMonitorTests
    {
        [Fact]
        public async Task TcpMonitor_ReturnsUp_WhenReachable()
        {
            TcpMonitor tcpMonitor = new() { Options = new() { Host = "192.168.10.15", Port = 32400 } };
            var result = await tcpMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);
        }

        [Fact]
        public async Task TcpMonitor_ReturnsDown_WhenUnreachable()
        {
            TcpMonitor tcpMonitor = new() { Options = new() { Host = "192.168.10.15", Port = 32401, Timeout = TimeSpan.FromMilliseconds(200) } };
            var result = await tcpMonitor.QueryAsync();
            Assert.Equal(MonitorState.Down, result);
        }
    }
}
