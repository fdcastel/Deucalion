using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors;

public class CheckInMonitorTests
{
    [Fact]
    public async Task CheckInMonitor_ReturnsUp_WhenCheckedIn()
    {
        CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) };
        checkInMonitor.CheckIn();
        var response = await checkInMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, response.State);
    }

    [Fact]
    public async Task CheckInMonitor_ReturnsDown_WhenNotCheckedIn()
    {
        CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) };
        checkInMonitor.CheckIn();
        var response = await checkInMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, response.State);
        await Task.Delay(checkInMonitor.IntervalToDown * 2);
        response = await checkInMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, response.State);
    }

    [Fact]
    public async Task CheckInMonitor_Returns_StatePassedAsArgument()
    {
        CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(500) };
        var newResponse = new MonitorResponse()
        {
            State = MonitorState.Down,
            ResponseText = "Lorem Ipsum",
            ResponseTime = TimeSpan.FromMilliseconds(500)
        };
        checkInMonitor.CheckIn(newResponse);
        var response = await checkInMonitor.QueryAsync();
        Assert.NotNull(response);
        Assert.Equal(newResponse.State, response.State);
        Assert.Equal(newResponse.ResponseText, response.ResponseText);
        Assert.Equal(newResponse.ResponseTime, response.ResponseTime);
        newResponse = newResponse with { State = MonitorState.Up };
        checkInMonitor.CheckIn(newResponse);
        response = await checkInMonitor.QueryAsync();
        Assert.NotNull(response);
        Assert.Equal(newResponse.State, response.State);
        await Task.Delay(checkInMonitor.IntervalToDown * 2);
        response = await checkInMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, response.State);
    }
}
