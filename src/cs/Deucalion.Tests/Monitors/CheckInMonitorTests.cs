using System.Threading.Channels;
using Deucalion.Events;
using Deucalion.Network.Monitors;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests.Monitors;

public class CheckInMonitorTests
{
    [Fact]
    public async Task CheckInMonitor_ReturnsUp_WhenCheckedIn()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(1000), TimeProvider = fakeTime };
        checkInMonitor.CheckIn();
        var response = await checkInMonitor.QueryAsync(cancellationToken);
        Assert.Equal(MonitorState.Up, response.State);
    }

    [Fact]
    public async Task CheckInMonitor_ReturnsDown_WhenNotCheckedIn()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(1000), TimeProvider = fakeTime };
        checkInMonitor.CheckIn();
        var response = await checkInMonitor.QueryAsync(cancellationToken);
        Assert.Equal(MonitorState.Up, response.State);
        fakeTime.Advance(checkInMonitor.IntervalToDown * 2);
        response = await checkInMonitor.QueryAsync(cancellationToken);
        Assert.Equal(MonitorState.Down, response.State);
    }

    [Fact]
    public async Task CheckInMonitor_Returns_StatePassedAsArgument()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        CheckInMonitor checkInMonitor = new() { IntervalToDown = TimeSpan.FromMilliseconds(1000), TimeProvider = fakeTime };
        var newResponse = new MonitorResponse()
        {
            State = MonitorState.Down,
            ResponseText = "Lorem Ipsum",
            ResponseTime = TimeSpan.FromMilliseconds(500)
        };
        checkInMonitor.CheckIn(newResponse);
        var response = await checkInMonitor.QueryAsync(cancellationToken);
        Assert.NotNull(response);
        Assert.Equal(newResponse.State, response.State);
        Assert.Equal(newResponse.ResponseText, response.ResponseText);
        Assert.Equal(newResponse.ResponseTime, response.ResponseTime);
        newResponse = newResponse with { State = MonitorState.Up };
        checkInMonitor.CheckIn(newResponse);
        response = await checkInMonitor.QueryAsync(cancellationToken);
        Assert.NotNull(response);
        Assert.Equal(newResponse.State, response.State);
        fakeTime.Advance(checkInMonitor.IntervalToDown * 2);
        response = await checkInMonitor.QueryAsync(cancellationToken);
        Assert.Equal(MonitorState.Down, response.State);
    }

    [Fact]
    public async Task CheckInMonitor_CheckIn_TriggersImmediateStateUpdate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        var monitor = new CheckInMonitor { IntervalToDown = TimeSpan.FromSeconds(10), TimeProvider = fakeTime };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start the monitor loop
        var monitorTask = Deucalion.Application.MonitorExtensions.RunAsync(monitor, channel.Writer, cts.Token);

        // Wait for the first event (should be Down since no check-in yet)
        var evt1 = await channel.Reader.ReadAsync(cts.Token);
        Assert.True(evt1 is MonitorChecked mc1 && mc1.Response?.State == MonitorState.Down);

        // Call CheckIn — this cancels the polling delay, causing an immediate re-query
        monitor.CheckIn();

        // The loop should re-run quickly; read the next event (expect Up)
        var sawUp = false;
        // Read a few events looking for an Up state
        for (var i = 0; i < 5; i++)
        {
            var evt = await channel.Reader.ReadAsync(cts.Token);
            if (evt is MonitorChecked mc2 && mc2.Response?.State == MonitorState.Up)
            {
                sawUp = true;
                break;
            }
        }
        Assert.True(sawUp, "Did not receive an Up event after CheckIn()");

        cts.Cancel();
        try { await monitorTask; } catch (OperationCanceledException) { }
    }
}
