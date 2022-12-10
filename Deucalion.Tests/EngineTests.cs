using System.Collections.Concurrent;
using Deucalion.Monitors;
using Deucalion.Monitors.Events;
using Deucalion.Tests.Mocks;
using Xunit;
using Xunit.Abstractions;

namespace Deucalion.Tests;

public class EngineTests
{
    private readonly ITestOutputHelper _output;

    public EngineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Engine_ReceiveEventsFromPushMonitors()
    {
        var pulse = TimeSpan.FromMilliseconds(500);

        Engine engine = new();

        using CheckInMonitor m1 = new() { Name = "m1", IntervalToDown = pulse * 1.1 };
        using CheckInMonitor m2 = new() { Name = "m2", IntervalToDown = pulse * 1.1 };

        var eventCount = new ConcurrentDictionary<Type, int>();

        void MonitorCallback(MonitorEvent monitorEvent)
        {
            _output.WriteLine(monitorEvent.ToString());
            eventCount.AddOrUpdate(monitorEvent.GetType(), 1, (k, v) => Interlocked.Increment(ref v));
        }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        Task.Run(async () =>
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
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        try
        {
            using CancellationTokenSource cts = new(pulse * 4.5);
            var monitors = new List<Deucalion.Monitors.Monitor>() { m1, m2 };
            engine.Run(monitors, MonitorCallback, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // NOP
        }

        Assert.Equal(6, eventCount[typeof(CheckedIn)]);
        Assert.Equal(2, eventCount[typeof(CheckInMissed)]);
        Assert.Equal(4, eventCount[typeof(StateChanged)]);
    }

    [Fact]
    public void Engine_QueryPullMonitors()
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

        void MonitorCallback(MonitorEvent monitorEvent)
        {
            _output.WriteLine(monitorEvent.ToString());
            eventCount.AddOrUpdate(monitorEvent.GetType(), 1, (k, v) => Interlocked.Increment(ref v));
        }

        try
        {
            m1.Start();
            m2.Start();

            using CancellationTokenSource cts = new(pulse * 4.5);
            var monitors = new List<Deucalion.Monitors.Monitor>() { m1, m2 };
            engine.Run(monitors, MonitorCallback, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // NOP
        }

        Assert.Equal(10, eventCount[typeof(QueryResponse)]);
        Assert.Equal(3, eventCount[typeof(StateChanged)]);
    }

    [Fact]
    public void Engine_QueryPullMonitors_WithDifferentIntervalWhenDown()
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

        void MonitorCallback(MonitorEvent monitorEvent)
        {
            _output.WriteLine(monitorEvent.ToString());
            eventCount.AddOrUpdate(monitorEvent.GetType(), 1, (k, v) => Interlocked.Increment(ref v));
        }

        try
        {
            m1.Start();

            using CancellationTokenSource cts = new(pulse * 7.5);
            var monitors = new List<Deucalion.Monitors.Monitor>() { m1 };
            engine.Run(monitors, MonitorCallback, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // NOP
        }

        Assert.Equal(14, eventCount[typeof(QueryResponse)]);
        Assert.Equal(2, eventCount[typeof(StateChanged)]);
    }
}
