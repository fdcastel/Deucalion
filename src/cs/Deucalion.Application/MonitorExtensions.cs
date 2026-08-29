using System.Threading.Channels;
using Deucalion.Events;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;

namespace Deucalion.Application;

public static class MonitorExtensions
{
    public static async Task RunAllAsync(this IEnumerable<PullMonitor> monitors, ChannelWriter<IMonitorEvent> writer, CancellationToken stopToken)
    {
        try
        {
            var allTasks = monitors.Select(monitor => RunMonitorSafeAsync(monitor, writer, stopToken)).ToList();
            await Task.WhenAll(allTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static async Task RunMonitorSafeAsync(PullMonitor monitor, ChannelWriter<IMonitorEvent> writer, CancellationToken stopToken)
    {
        try
        {
            await monitor.RunAsync(writer, stopToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception)
        {
            // Backstop only. RunAsync already turns a failing probe into a Down result, so
            // reaching here means the loop itself failed -- keep it from taking down Task.WhenAll.
        }
    }

    /// <summary>
    /// Poll interval for the given state. Up-ish states (including Warn -- "up but slow")
    /// use the relaxed interval; failing states are re-probed sooner.
    /// </summary>
    public static TimeSpan DelayFor(PullMonitor monitor, MonitorState state) =>
        state is MonitorState.Up or MonitorState.Warn or MonitorState.Unknown
            ? monitor.IntervalWhenUp
            : monitor.IntervalWhenDown;

    public static async Task RunAsync(this PullMonitor monitor, ChannelWriter<IMonitorEvent> writer, CancellationToken stopToken)
    {
        var lastKnownState = MonitorState.Unknown;
        var consecutiveFailCount = 0;
        while (!stopToken.IsCancellationRequested)
        {
            var queryStartTime = monitor.TimeProvider.GetUtcNow();
            var startTimestamp = monitor.TimeProvider.GetTimestamp();

            MonitorResponse response;
            try
            {
                response = await monitor.QueryAsync(stopToken);
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A probe that throws is a failed probe, not a dead monitor. Previously this
                // escaped the loop and silently stopped polling for the process lifetime --
                // reachable today via an invalid expectedResponseBodyPattern.
                response = MonitorResponse.Down(monitor.TimeProvider.GetElapsedTime(startTimestamp), ex.Message);
            }

            if (response.ResponseTime is null)
            {
                response = response with { ResponseTime = monitor.TimeProvider.GetElapsedTime(startTimestamp) };
            }

            var name = monitor.Name;
            var initialState = response.State;
            var effectiveState = initialState;

            // Warn is "up but slow", not a failure: counting it would let IgnoreFailCount
            // report a merely-slow monitor as Degraded ("May be down") in the UI.
            if (initialState is MonitorState.Up or MonitorState.Warn)
            {
                consecutiveFailCount = 0;
            }
            else if (initialState == MonitorState.Down)
            {
                consecutiveFailCount++;
                if (monitor.IgnoreFailCount > 0 && consecutiveFailCount < monitor.IgnoreFailCount)
                {
                    effectiveState = MonitorState.Degraded;
                }
            }

            if (monitor.UpsideDown)
            {
                if (effectiveState == MonitorState.Up)
                {
                    effectiveState = MonitorState.Down;
                }
                else if (effectiveState == MonitorState.Down)
                {
                    effectiveState = MonitorState.Up;
                }
            }

            var effectiveResponse = response with { State = effectiveState };
            writer.TryWrite(new MonitorChecked(name, queryStartTime, lastKnownState, effectiveResponse));

            var actualStateHasChanged = lastKnownState != effectiveState;
            if (actualStateHasChanged)
            {
                writer.TryWrite(new MonitorStateChanged(name, queryStartTime, lastKnownState, effectiveState));
            }

            lastKnownState = effectiveState;

            if (stopToken.IsCancellationRequested) break;

            var delayInterval = DelayFor(monitor, lastKnownState);

            try
            {
                if (monitor is CheckInMonitor checkInMonitor)
                {
                    // Support short-circuit for CheckInMonitor.
                    //
                    // Both sources must be disposed: the linked source registers a callback on
                    // the application-lifetime stopToken, and that registration is only released
                    // on Dispose(). Leaking one per poll grew stopToken's callback list forever
                    // (~1440/day per check-in monitor at the 60s default) and made every
                    // subsequent CreateLinkedTokenSource lock a longer list.
                    //
                    // The monitor must forget the source before it is disposed, or a check-in
                    // arriving between iterations targets a disposed source and is not
                    // short-circuited (issue #22). The finally runs before the `using` disposals.
                    using var delayCts = new CancellationTokenSource();
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stopToken, delayCts.Token);
                    try
                    {
                        // A check-in that landed while no source was armed (during the probe
                        // above) is caught here instead of waiting out a full interval.
                        if (checkInMonitor.ArmDelay(delayCts))
                        {
                            await Task.Delay(delayInterval, monitor.TimeProvider, linkedCts.Token);
                        }
                    }
                    finally
                    {
                        checkInMonitor.DisarmDelay();
                    }
                }
                else
                {
                    await Task.Delay(delayInterval, monitor.TimeProvider, stopToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Exit delay early if cancelled
            }
        }
    }
}
