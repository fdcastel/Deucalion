using System.Threading.Channels;
using Deucalion.Events;
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

    [Fact]
    public async Task CheckInMonitor_CheckIn_TriggersImmediateStateUpdate()
    {
        var monitor = new CheckInMonitor { IntervalToDown = TimeSpan.FromSeconds(10) };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Start the monitor loop
        var monitorTask = Deucalion.Application.MonitorExtensions.RunAsync(monitor, channel.Writer, cts.Token);

        // Wait for the first event (should be Down)
        var evt1 = await channel.Reader.ReadAsync(cts.Token);
        Assert.True(evt1 is MonitorChecked mc1 && mc1.Response?.State == MonitorState.Down);

        // Call CheckIn and expect an Up event soon after
        monitor.CheckIn();
        var sawUp = false;
        var timeout = Task.Delay(1000, cts.Token);
        while (!sawUp)
        {
            var readTask = channel.Reader.ReadAsync(cts.Token).AsTask();
            var completed = await Task.WhenAny(readTask, timeout);
            if (completed == timeout) break;
            var evt = await readTask;
            if (evt is MonitorChecked mc2 && mc2.Response?.State == MonitorState.Up)
            {
                sawUp = true;
                break;
            }
        }
        Assert.True(sawUp, "Did not receive an Up event after CheckIn() within timeout");

        cts.Cancel();
        await Task.WhenAny(monitorTask, Task.Delay(500));
    }
}
