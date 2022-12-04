using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class PingMonitorTests
    {
        [Fact]
        public async Task PingMonitor_ReturnsTrue_WhenReachable()
        {
            PingMonitor pingMonitor = new() { Options = new() { Host = "192.168.10.1" } };
            bool result = await pingMonitor.IsUpAsync();
            Assert.True(result);
        }

        [Fact]
        public async Task PingMonitor_ReturnsFalse_WhenUnreachable()
        {
            PingMonitor pingMonitor = new() { Options = new() { Host = "192.168.1.1", Timeout = 200 } };
            bool result = await pingMonitor.IsUpAsync();
            Assert.False(result);
        }
    }
}