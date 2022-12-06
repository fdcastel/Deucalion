using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class CheckInMonitorTests
    {
        [Fact]
        public async Task CheckInMonitor_ReturnsUnknown_WhenInitialized()
        {
            CheckInMonitor checkInMonitor = new() { Options = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) } };
            var result = await checkInMonitor.QueryAsync();
            Assert.Equal(MonitorState.Unknown, result);
        }

        [Fact]
        public async Task CheckInMonitor_ReturnsUp_WhenCheckedIn()
        {
            CheckInMonitor checkInMonitor = new() { Options = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) } };
            checkInMonitor.CheckIn();
            var result = await checkInMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);
        }

        [Fact]
        public async Task CheckInMonitor_ReturnsDown_WhenNotCheckedIn()
        {
            CheckInMonitor checkInMonitor = new() { Options = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) } };
            checkInMonitor.CheckIn();
            var result = await checkInMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);

            await Task.Delay(checkInMonitor.Options.IntervalToDownOrDefault * 2);

            result = await checkInMonitor.QueryAsync();
            Assert.Equal(MonitorState.Down, result);
        }
    }
}
