using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class CheckInMonitorTests
    {
        [Fact]
        public void CheckInMonitor_ReturnsUnknown_WhenInitialized()
        {
            using CheckInMonitor checkInMonitor = new() { Options = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) } };
            var result = MonitorState.Unknown;
            checkInMonitor.CheckedInEvent += (s, a) => result = MonitorState.Up;
            checkInMonitor.TimedOutEvent += (s, a) => result = MonitorState.Down;
            Assert.Equal(MonitorState.Unknown, result);
        }

        [Fact]
        public void CheckInMonitor_ReturnsUp_WhenCheckedIn()
        {
            using CheckInMonitor checkInMonitor = new() { Options = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) } };
            var result = MonitorState.Unknown;
            checkInMonitor.CheckedInEvent += (s, a) => result = MonitorState.Up;
            checkInMonitor.TimedOutEvent += (s, a) => result = MonitorState.Down;

            checkInMonitor.CheckIn();
            Assert.Equal(MonitorState.Up, result);
        }

        [Fact]
        public async Task CheckInMonitor_ReturnsDown_WhenNotCheckedIn()
        {
            using CheckInMonitor checkInMonitor = new() { Options = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) } };
            var result = MonitorState.Unknown;
            checkInMonitor.CheckedInEvent += (s, a) => result = MonitorState.Up;
            checkInMonitor.TimedOutEvent += (s, a) => result = MonitorState.Down;

            checkInMonitor.CheckIn();
            Assert.Equal(MonitorState.Up, result);

            await Task.Delay(checkInMonitor.Options.IntervalToDownOrDefault * 2);

            Assert.Equal(MonitorState.Down, result);
        }

        [Fact]
        public async Task CheckInMonitor_Returns_StatePassedAsArgument()
        {
            using CheckInMonitor checkInMonitor = new() { Options = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) } };
            var result = MonitorState.Unknown;
            checkInMonitor.CheckedInEvent += (s, a) => result = a is MonitorResponse mr ? mr.State : throw new InvalidOperationException();
            checkInMonitor.TimedOutEvent += (s, a) => result = MonitorState.Down;

            checkInMonitor.CheckIn(new() { State = MonitorState.Down });
            Assert.Equal(MonitorState.Down, result);

            checkInMonitor.CheckIn(new() { State = MonitorState.Up });
            Assert.Equal(MonitorState.Up, result);

            await Task.Delay(checkInMonitor.Options.IntervalToDownOrDefault * 2);

            Assert.Equal(MonitorState.Down, result);
        }
    }
}
