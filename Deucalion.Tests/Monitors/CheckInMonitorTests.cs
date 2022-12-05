using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class CheckInMonitorTests
    {
        [Fact]
        public async Task CheckInMonitor_ReturnsFalse_WhenInitialized()
        {
            CheckInMonitor checkInMonitor = new() { Options = new() };
            var result = await checkInMonitor.IsUpAsync();
            Assert.False(result);
        }

        [Fact]
        public async Task CheckInMonitor_ReturnsTrue_WhenCheckedIn()
        {
            CheckInMonitor checkInMonitor = new() { Options = new() };
            checkInMonitor.CheckIn();
            var result = await checkInMonitor.IsUpAsync();
            Assert.True(result);
        }

        [Fact]
        public async Task CheckInMonitor_ReturnsFalse_WhenNotCheckedIn()
        {
            CheckInMonitor checkInMonitor = new() { Options = new() { IntervalWhenUp = TimeSpan.FromMilliseconds(500) } };
            checkInMonitor.CheckIn();
            var result = await checkInMonitor.IsUpAsync();
            Assert.True(result);

            await Task.Delay(checkInMonitor.Options.IntervalWhenUpOrDefault * 2);

            result = await checkInMonitor.IsUpAsync();
            Assert.False(result);
        }
    }
}