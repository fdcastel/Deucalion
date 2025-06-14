using System.Threading.Channels;
using Deucalion.Events;
using Deucalion.Monitors;

namespace Deucalion.Application;

public static class MonitorExtensions
{
    public static async Task RunAllAsync(this IEnumerable<PullMonitor> monitors, ChannelWriter<IMonitorEvent> writer, CancellationToken stopToken)
    {
        try
        {
            var allTasks = monitors.Select(monitor => monitor.RunAsync(writer, stopToken)).ToList();
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

    public static async Task RunAsync(this PullMonitor monitor, ChannelWriter<IMonitorEvent> writer, CancellationToken stopToken)
    {
        var lastKnownState = MonitorState.Unknown;
        var consecutiveFailCount = 0;
        do
        {
            if (stopToken.IsCancellationRequested) break;

            var queryStartTime = DateTimeOffset.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await monitor.QueryAsync(stopToken);

            if (response.ResponseTime is null)
            {
                response = response with { ResponseTime = stopwatch.Elapsed };
            }

            var name = monitor.Name;
            var initialState = response?.State ?? MonitorState.Down;
            var effectiveState = initialState;

            if (initialState == MonitorState.Up)
            {
                consecutiveFailCount = 0;
            }
            else if (initialState == MonitorState.Down || initialState == MonitorState.Warn)
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

            var effectiveResponse = response is null ? null : response with { State = effectiveState };
            writer.TryWrite(new MonitorChecked(name, queryStartTime, effectiveResponse));

            var actualStateHasChanged = lastKnownState != effectiveState && lastKnownState == MonitorState.Unknown ||
                                        lastKnownState != MonitorState.Unknown && lastKnownState != effectiveState;

            if (actualStateHasChanged)
            {
                writer.TryWrite(new MonitorStateChanged(name, queryStartTime, effectiveState));
            }

            lastKnownState = effectiveState;

            if (stopToken.IsCancellationRequested) break;

            TimeSpan delayInterval = (lastKnownState == MonitorState.Up || lastKnownState == MonitorState.Unknown)
                ? monitor.IntervalWhenUp
                : monitor.IntervalWhenDown;

            try
            {
                await Task.Delay(delayInterval, stopToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        while (!stopToken.IsCancellationRequested);
    }
}
