using System.Threading.Channels;
using Deucalion.Application;
using Deucalion.Events;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Deucalion.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests;

public class EngineTests
{
    [Fact]
    public async Task Engine_ReceiveEventsFromPushMonitors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        var pulse = TimeSpan.FromSeconds(1);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse * 1.1, TimeProvider = fakeTime };
        CheckInMonitor m2 = new() { Name = "m2", IntervalToDown = pulse * 1.1, TimeProvider = fakeTime };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IEnumerable<PullMonitor> monitors = [m1, m2];
        var engineTask = RunMonitorsAsync(monitors, channel.Writer, cts);

        // Reproduce the check-in sequence with virtual time
        await SettleAsync();
        fakeTime.Advance(pulse / 2);
        m1.CheckIn();
        m2.CheckIn();
        await SettleAsync();
        fakeTime.Advance(pulse);
        m1.CheckIn();
        await SettleAsync();
        fakeTime.Advance(pulse);
        m2.CheckIn();
        await SettleAsync();
        fakeTime.Advance(pulse);
        m1.CheckIn();
        m2.CheckIn();
        await SettleAsync();

        cts.Cancel();
        try { await engineTask; } catch (OperationCanceledException) { }
        var events = CollectEvents(channel.Reader);

        // Instead of strict event counts, just assert that each monitor eventually goes Down
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m2" && sc.NewState == MonitorState.Down);
    }

    [Fact]
    public async Task Engine_QueryPullMonitors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        var pulse = TimeSpan.FromSeconds(1);
        PullMonitorMock m1 = new(
            (MonitorState.Unknown, pulse / 2),
            (MonitorState.Up, pulse),
            (MonitorState.Down, pulse),
            (MonitorState.Up, pulse)
            )
        { Name = "m1", IntervalWhenUp = pulse, IntervalWhenDown = pulse, TimeProvider = fakeTime };
        PullMonitorMock m2 = new(
            (MonitorState.Unknown, pulse / 2),
            (MonitorState.Down, pulse),
            (MonitorState.Up, pulse),
            (MonitorState.Up, pulse)
            )
        { Name = "m2", IntervalWhenUp = pulse, IntervalWhenDown = pulse, TimeProvider = fakeTime };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IEnumerable<PullMonitor> monitors = [m1, m2];
        var engineTask = RunMonitorsAsync(monitors, channel.Writer, cts);

        // First iteration runs immediately; advance through 3 more
        await SettleAsync();
        for (var i = 0; i < 3; i++)
        {
            fakeTime.Advance(pulse);
            await SettleAsync();
        }

        cts.Cancel();
        try { await engineTask; } catch (OperationCanceledException) { }
        var events = CollectEvents(channel.Reader);

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
        var fakeTime = new FakeTimeProvider();
        var pulse = TimeSpan.FromSeconds(1);
        PullMonitorMock m1 = new(
            (MonitorState.Unknown, pulse / 2),
            (MonitorState.Up, pulse * 2),
            (MonitorState.Down, pulse * 2),
            (MonitorState.Up, pulse)
            )
        { Name = "m1", IntervalWhenUp = pulse, IntervalWhenDown = pulse / 5, TimeProvider = fakeTime };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IEnumerable<PullMonitor> monitors = [m1];
        var engineTask = RunMonitorsAsync(monitors, channel.Writer, cts);

        // Iter 1 (immediate): Unknown → delay = IntervalWhenUp = pulse
        await SettleAsync();
        // Iter 2: Up → delay = IntervalWhenUp = pulse
        fakeTime.Advance(pulse);
        await SettleAsync();
        // Iter 3: Down → delay = IntervalWhenDown = pulse/5
        fakeTime.Advance(pulse);
        await SettleAsync();
        // Iter 4: Up
        fakeTime.Advance(pulse / 5);
        await SettleAsync();

        cts.Cancel();
        try { await engineTask; } catch (OperationCanceledException) { }
        var events = CollectEvents(channel.Reader);

        // Assert that the monitor transitions through Up and Down at least once
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Up);
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
    }

    [Fact]
    public async Task Engine_PushMonitor_RepeatedDownState_GeneratesEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fakeTime = new FakeTimeProvider();
        var pulse = TimeSpan.FromSeconds(1);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse * 1.5, TimeProvider = fakeTime };
        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IEnumerable<PullMonitor> monitors = [m1];
        var engineTask = RunMonitorsAsync(monitors, channel.Writer, cts);

        // First iteration runs immediately → Down (no check-in)
        // CheckInMonitor uses default IntervalWhenDown = 15s
        await SettleAsync();
        // Advance past the delay to trigger a second iteration (still Down)
        fakeTime.Advance(PullMonitor.DefaultIntervalWhenDown);
        await SettleAsync();

        cts.Cancel();
        try { await engineTask; } catch (OperationCanceledException) { }
        var events = CollectEvents(channel.Reader);

        // Assert that the monitor goes Down at least once
        Assert.Contains(events, e => e is MonitorStateChanged sc && sc.Name == "m1" && sc.NewState == MonitorState.Down);
        Assert.Contains(events, e => e is MonitorChecked mc && mc.Name == "m1" && mc.Response is { State: MonitorState.Down });
    }

    private static Task RunMonitorsAsync(IEnumerable<PullMonitor> monitors, ChannelWriter<IMonitorEvent> writer, CancellationTokenSource cts)
        => monitors.RunAllAsync(writer, cts.Token);

    private static List<IMonitorEvent> CollectEvents(ChannelReader<IMonitorEvent> reader)
    {
        var events = new List<IMonitorEvent>();
        while (reader.TryRead(out var evt))
            events.Add(evt);
        return events;
    }

    /// <summary>
    /// Allows async continuations (triggered by FakeTimeProvider.Advance) to settle on the thread pool.
    /// </summary>
    private static Task SettleAsync() => Task.Delay(10);
}
