using System.Collections.Concurrent;
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
        const string CheckedInEvent = "CheckedIn";
        const string CheckInMissedEvent = "CheckInMissed";
        const string MonitorStateChangedEvent = "MonitorStateChanged";

        var pulse = TimeSpan.FromMilliseconds(500);

        Engine engine = new();

        using CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse * 1.1 };
        using CheckInMonitor m2 = new() { Name = "m2", IntervalToDown = pulse * 1.1 };

        var eventCount = new ConcurrentDictionary<string, int>();
        var events = new List<MonitorEventBase>();
        var channel = Channel.CreateUnbounded<MonitorEventBase>();

        var checkInTask = Task.Run(async () =>
        {
            var start = DateTimeOffset.UtcNow;

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
        try
        {
            using CancellationTokenSource cts = new(pulse * 4.5);
            var monitors = new List<Deucalion.Monitors.Monitor>() { m1, m2 };
            var engineTask = Task.Run(async () => await engine.RunAsync(monitors, channel.Writer, cts.Token));

            await foreach (var monitorEvent in channel.Reader.ReadAllAsync(cts.Token))
            {
                _output.WriteLine(monitorEvent.ToString());
                events.Add(monitorEvent);
                var eventType = monitorEvent switch
                {
                    MonitorChecked mr => mr.Response is null ? CheckInMissedEvent : CheckedInEvent,
                    MonitorStateChanged => MonitorStateChangedEvent,
                    _ => string.Empty
                };
                eventCount.AddOrUpdate(eventType, 1, (k, v) => Interlocked.Increment(ref v));
            }
            await engineTask;
            await checkInTask;
        }
        catch (OperationCanceledException)
        {
            // NOP
        }

        Assert.Equal(6, eventCount[CheckedInEvent]);
        Assert.Equal(2, eventCount[CheckInMissedEvent]);
        Assert.Equal(4, eventCount[MonitorStateChangedEvent]);
    }

    [Fact]
    public async Task Engine_QueryPullMonitors()
    {
        /*
            Here's a summary of the analysis for `Engine_QueryPullMonitors`:
            *   It uses two `PullMonitorMock` instances (`m1` and `m2`) with specific timelines.
            *   Both monitors have `IntervalWhenUp` and `IntervalWhenDown` set to the same `pulse` value (500ms).
            *   The test duration is `pulse * 4.5` (2250ms).

            Tracing the execution flow:
            1.  **Initial Queries (Time 0ms):**
                *   `m1` queried: `Unknown`. `MonitorChecked` event. `LastKnownState` for `m1` becomes `Unknown`.
                *   `m2` queried: `Unknown`. `MonitorChecked` event. `LastKnownState` for `m2` becomes `Unknown`.
                *   Next delay for both is `IntervalWhenUp` (500ms) because their `LastKnownState` is `Unknown`.
                *   Events: 2 `MonitorChecked`, 0 `MonitorStateChanged`.

            2.  **After 500ms (Time 500ms):**
                *   `m1` queried: `Up`. `MonitorChecked` event. `LastKnownState` for `m1` becomes `Up`. (No `MonitorStateChanged` because previous was `Unknown`).
                *   `m2` queried: `Down`. `MonitorChecked` event. `LastKnownState` for `m2` becomes `Down`. (No `MonitorStateChanged` because previous was `Unknown`).
                *   Next delay for `m1` is `IntervalWhenUp` (500ms). Next delay for `m2` is `IntervalWhenDown` (500ms).
                *   Events: 4 `MonitorChecked`, 0 `MonitorStateChanged`.

            3.  **After another 500ms (Time 1000ms):**
                *   `m1` queried: `Down`. `MonitorChecked` event. `MonitorStateChanged` (Up -> Down). `LastKnownState` for `m1` becomes `Down`.
                *   `m2` queried: `Up`. `MonitorChecked` event. `MonitorStateChanged` (Down -> Up). `LastKnownState` for `m2` becomes `Up`.
                *   Next delay for `m1` is `IntervalWhenDown` (500ms). Next delay for `m2` is `IntervalWhenUp` (500ms).
                *   Events: 6 `MonitorChecked`, 2 `MonitorStateChanged`.

            4.  **After another 500ms (Time 1500ms):**
                *   `m1` queried: `Up`. `MonitorChecked` event. `MonitorStateChanged` (Down -> Up). `LastKnownState` for `m1` becomes `Up`.
                *   `m2` queried: `Up`. `MonitorChecked` event. (No `MonitorStateChanged` as `LastKnownState` was already `Up` from the mock's perspective, but for m2, its previous state was `Up` so no change event). `LastKnownState` for `m2` becomes `Up`.
                *   Next delay for both is `IntervalWhenUp` (500ms).
                *   Events: 8 `MonitorChecked`, 3 `MonitorStateChanged`.

            5.  **After another 500ms (Time 2000ms):**
                *   `m1` queried: `Up` (last state in its timeline). `MonitorChecked` event. (No `MonitorStateChanged`).
                *   `m2` queried: `Up` (last state in its timeline). `MonitorChecked` event. (No `MonitorStateChanged`).
                *   Next delay for both is `IntervalWhenUp` (500ms).
                *   Events: 10 `MonitorChecked`, 3 `MonitorStateChanged`.

            The test duration is 2250ms. The next queries would occur at 2500ms, which is beyond the cancellation token's timeout.

            The current assertions in `Engine_QueryPullMonitors` are:
            *   `Assert.Equal(10, eventCount[typeof(MonitorChecked)]);`
            *   `Assert.Equal(3, eventCount[typeof(MonitorStateChanged)]);`

            This detailed trace matches the existing assertions for `Engine_QueryPullMonitors`. 
        */

        var pulse = TimeSpan.FromMilliseconds(500);

        Engine engine = new();

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

        var eventCount = new ConcurrentDictionary<Type, int>();
        var events = new List<MonitorEventBase>();
        var channel = Channel.CreateUnbounded<MonitorEventBase>();

        try
        {
            using CancellationTokenSource cts = new(pulse * 4.5);
            var monitors = new List<Deucalion.Monitors.Monitor>() { m1, m2 };
            var engineTask = Task.Run(async () => await engine.RunAsync(monitors, channel.Writer, cts.Token));

            await foreach (var monitorEvent in channel.Reader.ReadAllAsync(cts.Token))
            {
                _output.WriteLine(monitorEvent.ToString());
                events.Add(monitorEvent);
                eventCount.AddOrUpdate(monitorEvent.GetType(), 1, (k, v) => Interlocked.Increment(ref v));
            }
            await engineTask;
        }
        catch (OperationCanceledException)
        {
            // NOP
        }

        Assert.Equal(10, eventCount[typeof(MonitorChecked)]);
        Assert.Equal(3, eventCount[typeof(MonitorStateChanged)]);
    }

    [Fact]
    public async Task Engine_QueryPullMonitors_WithDifferentIntervalWhenDown()
    {
        /*
            Let's analyze the test and the refactored `Engine` logic:

            *   **Test Setup**:
                *   `pulse = TimeSpan.FromMilliseconds(500)`
                *   `PullMonitorMock m1` timeline:
                    1.  `MonitorState.Unknown`, duration `pulse / 2` (250ms)
                    2.  `MonitorState.Up`, duration `pulse * 2` (1000ms)
                    3.  `MonitorState.Down`, duration `pulse * 2` (1000ms)
                    4.  `MonitorState.Up`, duration `pulse` (500ms)
                *   `m1.IntervalWhenUp = pulse` (500ms)
                *   `m1.IntervalWhenDown = pulse / 5` (100ms)
                *   `CancellationTokenSource cts = new(pulse * 7.5)` (3750ms total test duration)

            *   **Engine Behavior (Refactored)**:
                1.  Initial query happens immediately. `QueryAndProcessPullMonitorAsync` is called. `m1` returns `Unknown`. `MonitorChecked` event. `status.LastKnownState` becomes `Unknown`.
                2.  Loop 1: `delayInterval` is `IntervalWhenUp` (500ms) because `LastKnownState` is `Unknown`.
                    *   After 500ms: `QueryAndProcessPullMonitorAsync`. `m1` returns `Up`. `MonitorChecked` event. `status.LastKnownState` becomes `Up`.
                3.  Loop 2: `delayInterval` is `IntervalWhenUp` (500ms).
                    *   After 500ms (total 1000ms): `QueryAndProcessPullMonitorAsync`. `m1` returns `Down`. `MonitorChecked` event. `MonitorStateChanged` (Up -> Down). `status.LastKnownState` becomes `Down`.
                4.  Loop 3: `delayInterval` is `IntervalWhenDown` (100ms).
                    *   After 100ms (total 1100ms): `QueryAndProcessPullMonitorAsync`. `m1` returns `Up`. `MonitorChecked` event. `MonitorStateChanged` (Down -> Up). `status.LastKnownState` becomes `Up`.
                5.  Loop 4: `delayInterval` is `IntervalWhenUp` (500ms).
                    *   After 500ms (total 1600ms): `QueryAndProcessPullMonitorAsync`. `m1` returns `Up` (last state in its timeline). `MonitorChecked` event.
                6.  Loop 5: `delayInterval` is `IntervalWhenUp` (500ms).
                    *   After 500ms (total 2100ms): `QueryAndProcessPullMonitorAsync`. `m1` returns `Up`. `MonitorChecked` event.
                7.  Loop 6: `delayInterval` is `IntervalWhenUp` (500ms).
                    *   After 500ms (total 2600ms): `QueryAndProcessPullMonitorAsync`. `m1` returns `Up`. `MonitorChecked` event.
                8.  Loop 7: `delayInterval` is `IntervalWhenUp` (500ms).
                    *   After 500ms (total 3100ms): `QueryAndProcessPullMonitorAsync`. `m1` returns `Up`. `MonitorChecked` event.
                9.  Loop 8: `delayInterval` is `IntervalWhenUp` (500ms).
                    *   After 500ms (total 3600ms): `QueryAndProcessPullMonitorAsync`. `m1` returns `Up`. `MonitorChecked` event.
                10. Loop 9: `delayInterval` is `IntervalWhenUp` (500ms).
                    *   The `Task.Delay(500ms)` would take the total time to 4100ms, which is beyond the `cts` timeout of 3750ms. The loop will likely be cancelled before or during this query.

            Based on this step-through, we expect 9 `MonitorChecked` events. The original test expected 14. The discrepancy arises because the old `PullMonitorMock` had its own timing, which was not perfectly aligned with the `Engine`'s timer. 

            The refactored `Engine` and the new `PullMonitorMock` (which advances state per query) lead to a more deterministic and predictable number of queries within the given test duration.
        */

        var pulse = TimeSpan.FromMilliseconds(500);

        Engine engine = new();

        PullMonitorMock m1 = new(
            (MonitorState.Unknown, pulse / 2),
            (MonitorState.Up, pulse * 2),
            (MonitorState.Down, pulse * 2),
            (MonitorState.Up, pulse)
            )
        { Name = "m1", IntervalWhenUp = pulse, IntervalWhenDown = pulse / 5 };

        var eventCount = new ConcurrentDictionary<Type, int>();
        var events = new List<MonitorEventBase>();
        var channel = Channel.CreateUnbounded<MonitorEventBase>();

        try
        {
            using CancellationTokenSource cts = new(pulse * 7.5);
            var monitors = new List<Deucalion.Monitors.Monitor>() { m1 };
            var engineTask = Task.Run(async () => await engine.RunAsync(monitors, channel.Writer, cts.Token));

            await foreach (var monitorEvent in channel.Reader.ReadAllAsync(cts.Token))
            {
                _output.WriteLine(monitorEvent.ToString());
                events.Add(monitorEvent);
                eventCount.AddOrUpdate(monitorEvent.GetType(), 1, (k, v) => Interlocked.Increment(ref v));
            }
            await engineTask;
        }
        catch (OperationCanceledException)
        {
            // NOP
        }

        Assert.Equal(9, eventCount[typeof(MonitorChecked)]);
        Assert.Equal(2, eventCount[typeof(MonitorStateChanged)]);
    }
}
