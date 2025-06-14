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
            var engineTask = Task.Run(() => engine.Run(monitors, channel.Writer, cts.Token));

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
            m1.Start();
            m2.Start();

            using CancellationTokenSource cts = new(pulse * 4.5);
            var monitors = new List<Deucalion.Monitors.Monitor>() { m1, m2 };
            var engineTask = Task.Run(() => engine.Run(monitors, channel.Writer, cts.Token));

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
            m1.Start();

            using CancellationTokenSource cts = new(pulse * 7.5);
            var monitors = new List<Deucalion.Monitors.Monitor>() { m1 };
            var engineTask = Task.Run(() => engine.Run(monitors, channel.Writer, cts.Token));

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

        Assert.Equal(14, eventCount[typeof(MonitorChecked)]);
        Assert.Equal(2, eventCount[typeof(MonitorStateChanged)]);
    }
}
