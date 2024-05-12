using System.Diagnostics;
using Deucalion.Monitors;
using Deucalion.Monitors.Events;

namespace Deucalion.Application;

public class Engine
{
    public void Run(IEnumerable<MonitorBase> monitors, Action<MonitorEventBase> callback, CancellationToken stopToken)
    {
        var start = DateTimeOffset.UtcNow;

        var catalog = monitors
            .Select(monitor => new MonitorStatus() { Monitor = monitor })
            .ToDictionary(ms => ms.Monitor);

        foreach (var (monitor, status) in catalog)
        {
            if (monitor is CheckInMonitor checkInMonitor)
            {
                checkInMonitor.CheckedInEvent += PushMonitorCheckedIn;
                checkInMonitor.TimedOutEvent += PushMonitorTimedOut;
            }
            else if (monitor is PullMonitor pullMonitor)
            {
                status.QueryTimer = new Timer(QueryPullMonitor, monitor, TimeSpan.Zero, pullMonitor.IntervalWhenUpOrDefault);
            }
        }

        try
        {
            stopToken.WaitHandle.WaitOne();
        }
        finally
        {
            foreach (var (_, status) in catalog)
            {
                status.QueryTimer?.Dispose();
            }
        }

        void PushMonitorCheckedIn(object? sender, EventArgs args)
        {
            if (sender is PushMonitor pushMonitor)
            {
                var monitorResponse = args is MonitorResponseEventArgs mrea ? mrea.Response : MonitorResponse.Up();
                UpdateMonitorState(pushMonitor, monitorResponse);
            }
        }

        void PushMonitorTimedOut(object? sender, EventArgs _)
        {
            if (sender is PushMonitor pushMonitor)
            {
                UpdateMonitorState(pushMonitor, null);
            }
        }

        async void QueryPullMonitor(object? sender)
        {
            var timerEventAt = DateTimeOffset.UtcNow;

            if (sender is PullMonitor pullMonitor)
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await pullMonitor.QueryAsync();
                stopwatch.Stop();

                response.ResponseTime ??= stopwatch.Elapsed;

                UpdateMonitorState(pullMonitor, response, timerEventAt);
            }
        }

        void UpdateMonitorState(MonitorBase monitor, MonitorResponse? monitorResponse, DateTimeOffset timerEventAt = default)
        {
            var name = monitor.Name;
            var at = DateTimeOffset.UtcNow;

            if (catalog.TryGetValue(monitor, out var status))
            {
                // Notify response
                callback(new MonitorChecked(name, at, monitorResponse));

                var newState = monitorResponse?.State ?? MonitorState.Down;
                var hasStateChanged = status.LastKnownState != MonitorState.Unknown && status.LastKnownState != newState;
                if (hasStateChanged)
                {
                    // Notify change
                    callback(new MonitorStateChanged(name, at, newState));

                    if (monitor is PullMonitor pullMonitor)
                    {
                        // Update timer interval
                        var dueTime = newState == MonitorState.Up
                            ? pullMonitor.IntervalWhenUpOrDefault
                            : pullMonitor.IntervalWhenDownOrDefault;

                        // Subtract from next dueTime the elapsed time since the current timer event.
                        var deltaUntilNow = DateTimeOffset.UtcNow - timerEventAt;
                        var remaining = dueTime - deltaUntilNow;

                        // Avoid exception when debugging with breakpoints.
                        remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;

                        status.QueryTimer?.Change(remaining, dueTime);
                    }
                }

                status.LastKnownState = newState;
            }
        }
    }

    internal class MonitorStatus
    {
        internal required MonitorBase Monitor { get; init; }
        internal Timer? QueryTimer { get; set; }
        internal MonitorState LastKnownState { get; set; } = MonitorState.Unknown;
    }
}
