using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors;

public class CheckInMonitorTests
{
    [Fact]
    public void CheckInMonitor_ReturnsUp_WhenCheckedIn()
    {
        using CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) };
        var result = MonitorState.Unknown;
        checkInMonitor.CheckedInEvent += (s, a) => result = MonitorState.Up;
        checkInMonitor.TimedOutEvent += (s, a) => result = MonitorState.Down;

        checkInMonitor.CheckIn();
        Assert.Equal(MonitorState.Up, result);
    }

    [Fact]
    public async Task CheckInMonitor_ReturnsDown_WhenNotCheckedIn()
    {
        using CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) };
        var result = MonitorState.Unknown;
        checkInMonitor.CheckedInEvent += (s, a) => result = MonitorState.Up;
        checkInMonitor.TimedOutEvent += (s, a) => result = MonitorState.Down;

        checkInMonitor.CheckIn();
        Assert.Equal(MonitorState.Up, result);

        await Task.Delay(checkInMonitor.IntervalToDownOrDefault * 2);

        Assert.Equal(MonitorState.Down, result);
    }

    [Fact]
    public async Task CheckInMonitor_Returns_StatePassedAsArgument()
    {
        using CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) };

        MonitorResponse? currentResponse = null;
        checkInMonitor.CheckedInEvent += (s, ea) => currentResponse = ea is MonitorResponseEventArgs mrea ? mrea.Response : throw new InvalidOperationException();
        checkInMonitor.TimedOutEvent += (s, ea) => currentResponse = MonitorResponse.Down();

        var newResponse = new MonitorResponse()
        {
            State = MonitorState.Down,
            ResponseText = "Lorem Ipsum",
            ResponseTime = TimeSpan.FromMilliseconds(500)
        };

        checkInMonitor.CheckIn(newResponse);
        Assert.NotNull(currentResponse);
        Assert.Equal(newResponse.State, currentResponse.State);
        Assert.Equal(newResponse.ResponseText, currentResponse.ResponseText);
        Assert.Equal(newResponse.ResponseTime, currentResponse.ResponseTime);

        newResponse.State = MonitorState.Up;
        checkInMonitor.CheckIn(newResponse);
        Assert.NotNull(currentResponse);
        Assert.Equal(newResponse.State, currentResponse.State);

        await Task.Delay(checkInMonitor.IntervalToDownOrDefault * 2);

        Assert.Equal(MonitorState.Down, currentResponse.State);
    }
}
