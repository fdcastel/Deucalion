using System.Reflection;
using System.Threading.Channels;
using Deucalion.Application;
using Deucalion.Events;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors;

/// <summary>
/// The check-in short-circuit path allocates two CancellationTokenSources per poll. Both must
/// be disposed: the linked source registers a callback on the application-lifetime stop token,
/// and that registration is only released on Dispose().
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
        // A long interval means the second probe can only arrive if CheckIn() cut the delay.
        var cancellationToken = TestContext.Current.CancellationToken;
        var monitor = new CheckInMonitor
        {
            Name = "m",
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

        // Poll CheckIn() until it lands on a live delay source, then expect a prompt second probe.
        MonitorChecked? second = null;
        while (second is null)
        {
            monitor.CheckIn();
            try
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(stopCts.Token);
                attempt.CancelAfter(TimeSpan.FromMilliseconds(200));
                second = await ReadNextCheckedAsync(channel.Reader, attempt.Token);
            }
            catch (OperationCanceledException) when (!stopCts.Token.IsCancellationRequested)
            {
                // The loop had not reached its delay yet; try again.
            }
        }

        Assert.Equal(MonitorState.Up, second.Response?.State);

        await stopCts.CancelAsync();
        try { await engineTask; } catch (OperationCanceledException) { }
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
