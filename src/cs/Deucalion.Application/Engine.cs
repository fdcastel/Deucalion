using System.Diagnostics;
using System.Threading.Channels;
using Deucalion.Events;
using Deucalion.Monitors;

namespace Deucalion.Application;

public class Engine
{
    public async Task RunAsync(IEnumerable<Monitors.Monitor> monitors, ChannelWriter<MonitorEventBase> writer, CancellationToken stopToken)
    {
        var catalog = monitors
            .Select(monitor => new MonitorStatus() { Monitor = monitor })
            .ToDictionary(ms => ms.Monitor);

        var monitorTasks = new List<Task>();

        foreach (var (monitor, status) in catalog)
        {
            if (monitor is PullMonitor pullMonitor)
            {
                // Run pull monitor in its own async loop
                monitorTasks.Add(RunPullMonitorLoopAsync(pullMonitor, status, writer, stopToken));
            }
        }

        try
        {
            // Wait for all monitor tasks to complete or for cancellation
            await Task.WhenAll(monitorTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopToken is signaled and tasks are cancelled
        }
        // Consider catching other specific exceptions from tasks if robust error handling per task is needed
        finally
        {
            // Ensure writer is completed when all operations are done or cancelled
            writer.TryComplete();
        }
    }

    private async Task RunPullMonitorLoopAsync(PullMonitor pullMonitor, MonitorStatus status, ChannelWriter<MonitorEventBase> writer, CancellationToken stopToken)
    {
        // Perform initial query without delay
        await QueryAndProcessPullMonitorAsync(pullMonitor, status, writer, stopToken);
        if (stopToken.IsCancellationRequested) return;

        while (!stopToken.IsCancellationRequested)
        {
            TimeSpan delayInterval = (status.LastKnownState == MonitorState.Up || status.LastKnownState == MonitorState.Unknown)
                ? pullMonitor.IntervalWhenUp
                : pullMonitor.IntervalWhenDown;

            try
            {
                await Task.Delay(delayInterval, stopToken);
            }
            catch (OperationCanceledException)
            {
                break; // Exit loop if cancellation is requested during delay
            }

            if (stopToken.IsCancellationRequested) break; // Check again after delay

            await QueryAndProcessPullMonitorAsync(pullMonitor, status, writer, stopToken);
        }
    }

    private async Task QueryAndProcessPullMonitorAsync(PullMonitor pullMonitor, MonitorStatus status, ChannelWriter<MonitorEventBase> writer, CancellationToken stopToken)
    {
        if (stopToken.IsCancellationRequested) return;

        var queryStartTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var response = await pullMonitor.QueryAsync(stopToken);
        stopwatch.Stop();

        if (response.ResponseTime is null)
        {
            response = response with { ResponseTime = stopwatch.Elapsed };
        }

        UpdateMonitorState(pullMonitor, response, writer, status, queryStartTime);
    }

    private void UpdateMonitorState(Monitors.Monitor monitor, MonitorResponse? monitorResponse, ChannelWriter<MonitorEventBase> writer, MonitorStatus status, DateTimeOffset eventTime)
    {
        var name = monitor.Name;

        var initialState = monitorResponse?.State ?? MonitorState.Down;
        var effectiveState = initialState;

        // --- ignoreFailCount Logic ---
        if (initialState == MonitorState.Up)
        {
            status.ConsecutiveFailCount = 0;
        }
        else if (initialState == MonitorState.Down || initialState == MonitorState.Warn)
        {
            status.ConsecutiveFailCount++;
            if (monitor.IgnoreFailCount > 0 && status.ConsecutiveFailCount < monitor.IgnoreFailCount)
            {
                effectiveState = MonitorState.Degraded;
            }
            // else: effectiveState remains Down or Warn
        }
        // else: Unknown state doesn't change fail count or trigger Degraded

        // --- upsideDown Logic ---
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
            // Warn and Degraded states are not flipped
        }

        // Notify response
        var effectiveResponse = monitorResponse is null ? null : monitorResponse with { State = effectiveState };
        writer.TryWrite(new MonitorChecked(name, eventTime, effectiveResponse)); // Use eventTime

        // Send MonitorStateChanged only if the state actually changed from a known different state, or from Unknown.
        var actualStateHasChanged = status.LastKnownState != effectiveState && status.LastKnownState == MonitorState.Unknown || // From Unknown to something else
                                    status.LastKnownState != MonitorState.Unknown && status.LastKnownState != effectiveState;   // From a known state to a different known state

        if (actualStateHasChanged)
        {
            // Notify change
            writer.TryWrite(new MonitorStateChanged(name, eventTime, effectiveState)); // Use eventTime
        }

        status.LastKnownState = effectiveState;
    }

    internal class MonitorStatus
    {
        internal required Monitors.Monitor Monitor { get; init; }
        internal MonitorState LastKnownState { get; set; } = MonitorState.Unknown;
        internal int ConsecutiveFailCount { get; set; } = 0;
    }
}
