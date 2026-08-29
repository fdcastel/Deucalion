using System.Reflection;
using System.Threading.Channels;
using Deucalion.Application;
using Deucalion.Events;
using Deucalion.Network.Monitors;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests.Monitors;

/// <summary>
/// The check-in short-circuit path allocates two CancellationTokenSources per poll. Both must
/// be disposed: the linked source registers a callback on the application-lifetime stop token,
/// and that registration is only released on Dispose(). And a check-in must cut the delay short
/// no matter when it arrives relative to that per-poll lifetime (issue #22).
/// </summary>
public class CheckInMonitorDelayTests
{
    /// <summary>
    /// Reads the private registration list off a CancellationTokenSource. Each undisposed
    /// linked source leaves exactly one entry behind, so this grows without bound if the
    /// polling loop leaks them.
    /// </summary>
    private static int CountRegistrations(CancellationTokenSource source)
    {
        var registrations = typeof(CancellationTokenSource)
            .GetField("_registrations", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(source);

        if (registrations is null)
        {
            return 0;
        }

        var callbacks = registrations.GetType()
            .GetField("Callbacks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(registrations);

        var count = 0;
        var node = callbacks;
        while (node is not null)
        {
            count++;
            node = node.GetType()
                .GetField("Next", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(node);
        }

        return count;
    }

    [Fact]
    public async Task PollingLoop_DoesNotAccumulateRegistrationsOnTheStopToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var interval = TimeSpan.FromMilliseconds(1);
        var monitor = new CheckInMonitor
        {
            Name = "m",
            IntervalToDown = TimeSpan.FromMinutes(5),
            IntervalWhenUp = interval,
            IntervalWhenDown = interval,
        };

        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stopCts.CancelAfter(TimeSpan.FromSeconds(10));

        var engineTask = monitor.RunAsync(channel.Writer, stopCts.Token);

        // Let the loop complete a good number of poll cycles.
        const int Cycles = 200;
        var seen = 0;
        while (seen < Cycles)
        {
            var evt = await channel.Reader.ReadAsync(stopCts.Token);
            if (evt is MonitorChecked)
            {
                seen++;
            }
        }

        // Sample while the loop is still running: at most a couple of live registrations
        // (the current iteration's linked source, plus the test's own linked source).
        var registrations = CountRegistrations(stopCts);

        await stopCts.CancelAsync();
        try { await engineTask; } catch (OperationCanceledException) { }

        Assert.True(
            registrations < 10,
            $"Expected the stop token's registration list to stay small, but found {registrations} after {Cycles} poll cycles.");
    }

    [Fact]
    public async Task CheckIn_AfterTheLoopMovesOn_DoesNotThrow()
    {
        // The loop disposes each delay source, so a check-in racing that disposal must not
        // surface ObjectDisposedException out of the HTTP endpoint that called CheckIn().
        var cancellationToken = TestContext.Current.CancellationToken;
        var interval = TimeSpan.FromMilliseconds(1);
        var monitor = new CheckInMonitor
        {
            Name = "m",
            IntervalToDown = TimeSpan.FromMinutes(5),
            IntervalWhenUp = interval,
            IntervalWhenDown = interval,
        };

        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stopCts.CancelAfter(TimeSpan.FromSeconds(10));

        var engineTask = monitor.RunAsync(channel.Writer, stopCts.Token);

        // Hammer CheckIn() concurrently with the polling loop's dispose.
        var checkInTask = Task.Run(() =>
        {
            for (var i = 0; i < 2000; i++)
            {
                monitor.CheckIn();
            }
        }, cancellationToken);

        var drainTask = Task.Run(async () =>
        {
            while (!stopCts.Token.IsCancellationRequested)
            {
                try { await channel.Reader.ReadAsync(stopCts.Token); }
                catch (OperationCanceledException) { return; }
            }
        }, cancellationToken);

        await checkInTask; // must not throw

        await stopCts.CancelAsync();
        try { await engineTask; } catch (OperationCanceledException) { }
        try { await drainTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task CheckIn_ShortCircuitsThePollingDelay()
    {
        // Regression for issue #22. A 5-minute interval on a frozen clock means the second probe
        // can only arrive if the single CheckIn() below cut the delay short -- whether it landed
        // on the armed delay source or in the gap between iterations. Before the fix this test
        // had to call CheckIn() in a retry loop until one "landed on a live delay source".
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        var monitor = new CheckInMonitor
        {
            Name = "m",
            TimeProvider = time,
            IntervalToDown = TimeSpan.FromMinutes(5),
            IntervalWhenUp = TimeSpan.FromMinutes(5),
            IntervalWhenDown = TimeSpan.FromMinutes(5),
        };

        var channel = Channel.CreateUnbounded<IMonitorEvent>();
        using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stopCts.CancelAfter(TimeSpan.FromSeconds(10));

        var engineTask = monitor.RunAsync(channel.Writer, stopCts.Token);

        // First probe: no check-in yet, so Down.
        var first = await ReadNextCheckedAsync(channel.Reader, stopCts.Token);
        Assert.Equal(MonitorState.Down, first.Response?.State);

        monitor.CheckIn();

        // Exactly one check-in, no retries, and virtual time never advances.
        var second = await ReadNextCheckedAsync(channel.Reader, stopCts.Token);
        Assert.Equal(MonitorState.Up, second.Response?.State);

        await stopCts.CancelAsync();
        try { await engineTask; } catch (OperationCanceledException) { }
    }

    // The three tests below pin the ArmDelay/DisarmDelay contract the polling loop relies on,
    // in the exact interleavings issue #22 describes. They drive the monitor the way the loop
    // does, so they are deterministic instead of racing a background task.

    [Fact]
    public async Task CheckIn_WhileNoDelayIsArmed_IsReportedByTheNextArmDelay()
    {
        // The gap between iterations: the previous delay source is gone, the next is not yet
        // armed. The check-in must not wait out a full interval.
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitor = new CheckInMonitor { IntervalToDown = TimeSpan.FromMinutes(5), TimeProvider = new FakeTimeProvider() };

        monitor.CheckIn();

        using var delayCts = new CancellationTokenSource();
        Assert.False(monitor.ArmDelay(delayCts), "ArmDelay must report the check-in that arrived while nothing was armed.");
        Assert.False(delayCts.IsCancellationRequested, "The source armed after the check-in must not be cancelled.");
        monitor.DisarmDelay();

        // The probe consumes the pending check-in; a fresh delay then runs normally.
        var response = await monitor.QueryAsync(cancellationToken);
        Assert.Equal(MonitorState.Up, response.State);

        using var nextDelayCts = new CancellationTokenSource();
        Assert.True(monitor.ArmDelay(nextDelayCts));
        monitor.DisarmDelay();
    }

    [Fact]
    public void CheckIn_WhileADelayIsArmed_CancelsIt()
    {
        var monitor = new CheckInMonitor { IntervalToDown = TimeSpan.FromMinutes(5), TimeProvider = new FakeTimeProvider() };

        using var delayCts = new CancellationTokenSource();
        Assert.True(monitor.ArmDelay(delayCts));

        monitor.CheckIn();

        Assert.True(delayCts.IsCancellationRequested);
        monitor.DisarmDelay();
    }

    [Fact]
    public async Task CheckIn_AfterTheDelayIsDisarmedAndDisposed_IsStillRecorded()
    {
        // The loop has disarmed and disposed its source: CheckIn() must not touch the disposed
        // object, and the check-in must still reach the next probe.
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitor = new CheckInMonitor { IntervalToDown = TimeSpan.FromMinutes(5), TimeProvider = new FakeTimeProvider() };

        var delayCts = new CancellationTokenSource();
        Assert.True(monitor.ArmDelay(delayCts));
        monitor.DisarmDelay();
        delayCts.Dispose();

        monitor.CheckIn();

        using var nextDelayCts = new CancellationTokenSource();
        Assert.False(monitor.ArmDelay(nextDelayCts));
        monitor.DisarmDelay();

        var response = await monitor.QueryAsync(cancellationToken);
        Assert.Equal(MonitorState.Up, response.State);
    }

    private static async Task<MonitorChecked> ReadNextCheckedAsync(ChannelReader<IMonitorEvent> reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            var evt = await reader.ReadAsync(cancellationToken);
            if (evt is MonitorChecked checkedEvent)
            {
                return checkedEvent;
            }
        }
    }
}
