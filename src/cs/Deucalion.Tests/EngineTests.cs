using System.Threading.Channels;
using Deucalion.Application;
using Deucalion.Events;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Deucalion.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests;

/// <summary>
/// Covers <see cref="MonitorExtensions.RunAllAsync"/>: several monitors polling concurrently and
/// multiplexing into one channel. The per-monitor state machine is covered by
/// <see cref="MonitorStateMachineTests"/>.
/// </summary>
public class EngineTests
{
    /// <summary>
    /// Drives a set of monitors on a fake clock and lets tests wait for specific events rather
    /// than for a duration.
    /// </summary>
    private sealed class EngineHarness : IAsyncDisposable
    {
        private readonly Channel<IMonitorEvent> _channel = Channel.CreateUnbounded<IMonitorEvent>();
        private readonly CancellationTokenSource _cts;
        private readonly Task _engineTask;
        private readonly List<IMonitorEvent> _observed = [];

        public FakeTimeProvider Time { get; } = new();

        public EngineHarness(CancellationToken cancellationToken, params PullMonitor[] monitors)
        {
            foreach (var monitor in monitors)
            {
                monitor.TimeProvider = Time;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts.CancelAfter(TimeSpan.FromSeconds(20));
            _engineTask = monitors.RunAllAsync(_channel.Writer, _cts.Token);
        }

        /// <summary>
        /// Collects events until <paramref name="done"/> holds, advancing virtual time by
        /// <paramref name="step"/> whenever the reader has nothing left.
        /// </summary>
        /// <remarks>
        /// The advance is retried rather than issued once, because a monitor may not have reached
        /// its Task.Delay when the first advance lands -- a real race that a fixed sleep only
        /// papers over. Waiting on the events themselves is what makes this deterministic; the
        /// cost is that virtual time moves by an unknown number of steps, so tests assert on
        /// ordered state sequences rather than on raw event counts.
        /// </remarks>
        public async Task<IReadOnlyList<IMonitorEvent>> CollectUntilAsync(
            TimeSpan step, Func<IReadOnlyList<IMonitorEvent>, bool> done)
        {
            while (true)
            {
                while (_channel.Reader.TryRead(out var evt))
                {
                    _observed.Add(evt);
                }

                if (done(_observed))
                {
                    return _observed;
                }

                _cts.Token.ThrowIfCancellationRequested();
                Time.Advance(step);

                for (var i = 0; i < 20 && _channel.Reader.Count == 0; i++)
                {
                    await Task.Yield();
                }
            }
        }

        /// <summary>Waits until <paramref name="name"/> has produced <paramref name="count"/> state changes.</summary>
        public async Task<MonitorState[]> StateChangesAsync(string name, int count, TimeSpan step)
        {
            var events = await CollectUntilAsync(step, all => StatesOf(all, name).Length >= count);
            return [.. StatesOf(events, name).Take(count)];
        }

        private static MonitorState[] StatesOf(IReadOnlyList<IMonitorEvent> events, string name) =>
            [.. events.OfType<MonitorStateChanged>().Where(e => e.Name == name).Select(e => e.NewState)];

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            try { await _engineTask; } catch (OperationCanceledException) { }
            _cts.Dispose();
        }
    }

    [Fact]
    public async Task RunAllAsync_PollsEveryMonitorConcurrently()
    {
        var pulse = TimeSpan.FromSeconds(1);
        PullMonitorMock m1 = new((MonitorState.Up, pulse))
        { Name = "m1", IntervalWhenUp = pulse, IntervalWhenDown = pulse };
        PullMonitorMock m2 = new((MonitorState.Down, pulse))
        { Name = "m2", IntervalWhenUp = pulse, IntervalWhenDown = pulse };
        PullMonitorMock m3 = new((MonitorState.Warn, pulse))
        { Name = "m3", IntervalWhenUp = pulse, IntervalWhenDown = pulse };

        await using var harness = new EngineHarness(TestContext.Current.CancellationToken, m1, m2, m3);

        var events = await harness.CollectUntilAsync(pulse, all =>
            all.OfType<MonitorChecked>().Select(e => e.Name).Distinct().Count() == 3);

        // One channel carries all three monitors' events.
        Assert.Equal(["m1", "m2", "m3"], events.OfType<MonitorChecked>().Select(e => e.Name).Distinct().Order());
    }

    [Fact]
    public async Task RunAllAsync_ReportsEachMonitorsTransitionsInOrder()
    {
        var pulse = TimeSpan.FromSeconds(1);
        PullMonitorMock m1 = new(
            (MonitorState.Up, pulse), (MonitorState.Down, pulse), (MonitorState.Up, pulse))
        { Name = "m1", IntervalWhenUp = pulse, IntervalWhenDown = pulse };
        PullMonitorMock m2 = new(
            (MonitorState.Down, pulse), (MonitorState.Up, pulse), (MonitorState.Down, pulse))
        { Name = "m2", IntervalWhenUp = pulse, IntervalWhenDown = pulse };

        await using var harness = new EngineHarness(TestContext.Current.CancellationToken, m1, m2);

        // Exact ordered sequences, not "contains a Down somewhere".
        Assert.Equal(
            [MonitorState.Up, MonitorState.Down, MonitorState.Up],
            await harness.StateChangesAsync("m1", 3, pulse));
        Assert.Equal(
            [MonitorState.Down, MonitorState.Up, MonitorState.Down],
            await harness.StateChangesAsync("m2", 3, pulse));
    }

    [Fact]
    public async Task RunAllAsync_OneFailingMonitorDoesNotStopTheOthers()
    {
        var pulse = TimeSpan.FromSeconds(1);
        var boom = new ScriptedMonitor(_ => throw new InvalidOperationException("boom"))
        { Name = "boom", IntervalWhenUp = pulse, IntervalWhenDown = pulse };
        PullMonitorMock healthy = new((MonitorState.Up, pulse))
        { Name = "healthy", IntervalWhenUp = pulse, IntervalWhenDown = pulse };

        await using var harness = new EngineHarness(TestContext.Current.CancellationToken, boom, healthy);

        Assert.Equal([MonitorState.Up], await harness.StateChangesAsync("healthy", 1, pulse));
        // The throwing monitor keeps reporting rather than dropping out of the engine.
        Assert.Equal([MonitorState.Down], await harness.StateChangesAsync("boom", 1, pulse));
    }

    [Fact]
    public async Task CheckInMonitor_GoesDownWithoutACheckIn()
    {
        var pulse = TimeSpan.FromSeconds(1);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse };

        await using var harness = new EngineHarness(TestContext.Current.CancellationToken, m1);

        Assert.Equal([MonitorState.Down], await harness.StateChangesAsync("m1", 1, pulse));
    }

    [Fact]
    public async Task CheckInMonitor_GoesUpAfterACheckIn_AndBackDownWhenItStops()
    {
        var pulse = TimeSpan.FromSeconds(1);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse };

        await using var harness = new EngineHarness(TestContext.Current.CancellationToken, m1);

        // First probe: nothing has checked in yet.
        Assert.Equal([MonitorState.Down], await harness.StateChangesAsync("m1", 1, pulse));

        // Check in, then let the engine observe it. The check-in itself cuts the delay short
        // (issue #22), so the Up probe needs no clock advance -- and must not get one: the
        // re-probe runs on a pool thread, and with IntervalToDown == pulse, two advances landing
        // before it would make the check-in stale and the probe report Down (a 1-in-25 hang).
        m1.CheckIn();
        Assert.Equal(
            [MonitorState.Down, MonitorState.Up],
            await harness.StateChangesAsync("m1", 2, TimeSpan.Zero));

        // Stop checking in: once IntervalToDown lapses it drops back to Down.
        Assert.Equal(
            [MonitorState.Down, MonitorState.Up, MonitorState.Down],
            await harness.StateChangesAsync("m1", 3, pulse));
    }

    [Fact]
    public async Task CheckInMonitor_RepeatedDownProbesKeepEmittingCheckedEvents()
    {
        var pulse = TimeSpan.FromSeconds(1);
        CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse };

        await using var harness = new EngineHarness(TestContext.Current.CancellationToken, m1);

        // A monitor that stays Down still reports every probe -- only the state
        // *change* is emitted once.
        var events = await harness.CollectUntilAsync(pulse, all =>
            all.OfType<MonitorChecked>().Count(e => e.Name == "m1") >= 3);

        Assert.All(
            events.OfType<MonitorChecked>().Where(e => e.Name == "m1").Take(3),
            e => Assert.Equal(MonitorState.Down, e.Response?.State));
        Assert.Single(events.OfType<MonitorStateChanged>(), e => e.Name == "m1");
    }

    [Fact]
    public async Task IntervalWhenDown_IsUsedWhileAMonitorIsFailing()
    {
        // A short down-interval and a long up-interval: advancing by the short one is
        // enough to produce further probes only because the monitor is Down.
        var shortInterval = TimeSpan.FromSeconds(1);
        var longInterval = TimeSpan.FromMinutes(10);
        PullMonitorMock m1 = new((MonitorState.Down, shortInterval))
        { Name = "m1", IntervalWhenUp = longInterval, IntervalWhenDown = shortInterval };

        await using var harness = new EngineHarness(TestContext.Current.CancellationToken, m1);

        var events = await harness.CollectUntilAsync(shortInterval, all =>
            all.OfType<MonitorChecked>().Count(e => e.Name == "m1") >= 3);

        Assert.True(events.OfType<MonitorChecked>().Count(e => e.Name == "m1") >= 3);
        Assert.Equal(shortInterval, MonitorExtensions.DelayFor(m1, MonitorState.Down));
        Assert.Equal(longInterval, MonitorExtensions.DelayFor(m1, MonitorState.Up));
    }
}
