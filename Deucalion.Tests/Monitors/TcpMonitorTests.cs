using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class TcpMonitorTests
    {
        [Fact]
        public async Task TcpMonitor_ReturnsTrue_WhenReachable()
        {
            TcpMonitor tcpMonitor = new() { Options = new() { Host = "192.168.10.15", Port = 32400 } };
            bool result = await tcpMonitor.IsUpAsync();
            Assert.True(result);
        }

        [Fact]
        public async Task TcpMonitor_ReturnsFalse_WhenUnreachable()
        {
            TcpMonitor tcpMonitor = new() { Options = new() { Host = "192.168.10.15", Port = 32401, Timeout = TimeSpan.FromMilliseconds(200) } };
            bool result = await tcpMonitor.IsUpAsync();
            Assert.False(result);
        }
    }
}