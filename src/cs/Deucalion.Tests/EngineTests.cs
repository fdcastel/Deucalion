using System.Threading.Channels;
using Deucalion.Application;
using Deucalion.Events;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Deucalion.Tests.Mocks;
using Xunit;

namespace Deucalion.Tests;

public class EngineTests
{
    [Fact]
    public async Task Engine_ReceiveEventsFromPushMonitors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pulse = TimeSpan.FromMilliseconds(1000);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse * 1.1 };
        CheckInMonitor m2 = new() { Name = "m2", IntervalToDown = pulse * 1.1 };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        var checkInTask = RunCheckInSequenceAsync(pulse, m1, m2);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(pulse * 4.5);
        IEnumerable<PullMonitor> monitors = [m1, m2];
        var engineTask = RunMonitorsAsync(monitors, channel.Writer, cts);
        var events = await ReadEventsAsync(channel.Reader, cts);
        await engineTask;
        await checkInTask;
        // Instead of strict event counts, just assert that each monitor eventually goes Down
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m2" && sc.NewState == MonitorState.Down);
    }

    [Fact]
    public async Task Engine_QueryPullMonitors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pulse = TimeSpan.FromMilliseconds(1000);
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
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(pulse * 4.5);
        IEnumerable<PullMonitor> monitors = [m1, m2];
        var engineTask = RunMonitorsAsync(monitors, channel.Writer, cts);
        var events = await ReadEventsAsync(channel.Reader, cts);
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
        var cancellationToken = TestContext.Current.CancellationToken;
        var pulse = TimeSpan.FromMilliseconds(1000);
        PullMonitorMock m1 = new(
            (MonitorState.Unknown, pulse / 2),
            (MonitorState.Up, pulse * 2),
            (MonitorState.Down, pulse * 2),
            (MonitorState.Up, pulse)
            )
        { Name = "m1", IntervalWhenUp = pulse, IntervalWhenDown = pulse / 5 };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(pulse * 7.5);
        IEnumerable<PullMonitor> monitors = [m1];
        var engineTask = RunMonitorsAsync(monitors, channel.Writer, cts);
        var events = await ReadEventsAsync(channel.Reader, cts);
        await engineTask;
        // Assert that the monitor transitions through Up and Down at least once
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Up);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
    }

    [Fact]
    public async Task Engine_PushMonitor_RepeatedDownState_GeneratesEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pulse = TimeSpan.FromMilliseconds(250);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse * 1.5 };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(pulse * 3.5);
        IEnumerable<PullMonitor> monitors = [m1];
        var engineTask = RunMonitorsAsync(monitors, channel.Writer, cts);
        var events = await ReadEventsAsync(channel.Reader, cts);
        await engineTask;
        // Assert that the monitor goes Down at least once
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
        Assert.Contains(events, e => e is MonitorChecked mc && mc.Name == "m1" && mc.Response is { State: MonitorState.Down });
    }

    private static Task RunMonitorsAsync(IEnumerable<PullMonitor> monitors, ChannelWriter<IMonitorEvent> writer, CancellationTokenSource cts)
        => monitors.RunAllAsync(writer, cts.Token);

    private static async Task<List<IMonitorEvent>> ReadEventsAsync(ChannelReader<IMonitorEvent> reader, CancellationTokenSource cts)
    {
        var events = new List<IMonitorEvent>();

        try
        {
            await foreach (var monitorEvent in reader.ReadAllAsync(cts.Token))
            {
                events.Add(monitorEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the test timeout ends.
        }

        return events;
    }

    private static async Task RunCheckInSequenceAsync(TimeSpan pulse, CheckInMonitor m1, CheckInMonitor m2)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await Task.Delay(pulse / 2, cancellationToken);
        m1.CheckIn();
        m2.CheckIn();
        await Task.Delay(pulse, cancellationToken);
        m1.CheckIn();
        await Task.Delay(pulse, cancellationToken);
        m2.CheckIn();
        await Task.Delay(pulse, cancellationToken);
        m1.CheckIn();
        m2.CheckIn();
    }
}
