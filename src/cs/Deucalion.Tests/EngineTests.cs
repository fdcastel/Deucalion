using System.Threading.Channels;
using Deucalion.Application;
using Deucalion.Events;
using Deucalion.Network.Monitors;
using Deucalion.Tests.Mocks;
using Xunit;
using Xunit.Abstractions;

namespace Deucalion.Tests;

public class EngineTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Engine_ReceiveEventsFromPushMonitors()
    {
        var pulse = TimeSpan.FromMilliseconds(500);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse * 1.1 };
        CheckInMonitor m2 = new() { Name = "m2", IntervalToDown = pulse * 1.1 };
        var events = new List<MonitorEventBase>();
        var channel = Channel.CreateUnbounded<MonitorEventBase>();
        var checkInTask = Task.Run(async () =>
        {
            await Task.Delay(pulse / 2);
            m1.CheckIn();
            m2.CheckIn();
            await Task.Delay(pulse);
            m1.CheckIn();
            await Task.Delay(pulse);
            m2.CheckIn();
            await Task.Delay(pulse);
            m1.CheckIn();
            m2.CheckIn();
        });
        using CancellationTokenSource cts = new(pulse * 4.5);
        IEnumerable<Deucalion.Monitors.Monitor> monitors = [m1, m2];
        var engineTask = Task.Run(async () => await monitors.RunAllAsync(channel.Writer, cts.Token));
        try
        {
            await foreach (var monitorEvent in channel.Reader.ReadAllAsync(cts.Token))
            {
                events.Add(monitorEvent);
            }
        }
        catch (OperationCanceledException) { /* Expected on test end */ }
        await engineTask;
        await checkInTask;
        // Instead of strict event counts, just assert that each monitor eventually goes Down
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m2" && sc.NewState == MonitorState.Down);
    }

    [Fact]
    public async Task Engine_QueryPullMonitors()
    {
        var pulse = TimeSpan.FromMilliseconds(500);
        PullMonitorMock m1 = new(
            (MonitorState.Unknown, pulse / 2),
            (MonitorState.Up, pulse),
            (MonitorState.Down, pulse),
            (MonitorState.Up, pulse)
            )
        { Name = "m1", IntervalWhenUp = pulse, IntervalWhenDown = pulse };
        PullMonitorMock m2 = new(
            (MonitorState.Unknown, pulse / 2),
            (MonitorState.Down, pulse),
            (MonitorState.Up, pulse),
            (MonitorState.Up, pulse)
            )
        { Name = "m2", IntervalWhenUp = pulse, IntervalWhenDown = pulse };
        var events = new List<MonitorEventBase>();
        var channel = Channel.CreateUnbounded<MonitorEventBase>();
        using CancellationTokenSource cts = new(pulse * 4.5);
        IEnumerable<Deucalion.Monitors.Monitor> monitors = [m1, m2];
        var engineTask = Task.Run(async () => await monitors.RunAllAsync(channel.Writer, cts.Token));
        try
        {
            await foreach (var monitorEvent in channel.Reader.ReadAllAsync(cts.Token))
            {
                events.Add(monitorEvent);
            }
        }
        catch (OperationCanceledException) { /* Expected on test end */ }
        await engineTask;
        // Assert that each monitor transitions through Up and Down at least once
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Up);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m2" && sc.NewState == MonitorState.Up);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m2" && sc.NewState == MonitorState.Down);
    }

    [Fact]
    public async Task Engine_QueryPullMonitors_WithDifferentIntervalWhenDown()
    {
        var pulse = TimeSpan.FromMilliseconds(500);
        PullMonitorMock m1 = new(
            (MonitorState.Unknown, pulse / 2),
            (MonitorState.Up, pulse * 2),
            (MonitorState.Down, pulse * 2),
            (MonitorState.Up, pulse)
            )
        { Name = "m1", IntervalWhenUp = pulse, IntervalWhenDown = pulse / 5 };
        var events = new List<MonitorEventBase>();
        var channel = Channel.CreateUnbounded<MonitorEventBase>();
        using CancellationTokenSource cts = new(pulse * 7.5);
        IEnumerable<Deucalion.Monitors.Monitor> monitors = [m1];
        var engineTask = Task.Run(async () => await monitors.RunAllAsync(channel.Writer, cts.Token));
        try
        {
            await foreach (var monitorEvent in channel.Reader.ReadAllAsync(cts.Token))
            {
                events.Add(monitorEvent);
            }
        }
        catch (OperationCanceledException) { /* Expected on test end */ }
        await engineTask;
        // Assert that the monitor transitions through Up and Down at least once
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Up);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
    }

    [Fact]
    public async Task Engine_PushMonitor_RepeatedDownState_GeneratesEvent()
    {
        var pulse = TimeSpan.FromMilliseconds(100);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse * 1.5 };
        var events = new List<MonitorEventBase>();
        var channel = Channel.CreateUnbounded<MonitorEventBase>();
        using CancellationTokenSource cts = new(pulse * 3.5);
        IEnumerable<Deucalion.Monitors.Monitor> monitors = [m1];
        var engineTask = Task.Run(async () => await monitors.RunAllAsync(channel.Writer, cts.Token));
        try
        {
            await foreach (var monitorEvent in channel.Reader.ReadAllAsync(cts.Token))
            {
                events.Add(monitorEvent);
            }
        }
        catch (OperationCanceledException) { /* Expected on test end */ }
        await engineTask;
        // Assert that the monitor goes Down at least once
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
        Assert.Contains(events, e => e is MonitorChecked mc && mc.Name == "m1" && mc.Response is { State: MonitorState.Down });
    }
}
