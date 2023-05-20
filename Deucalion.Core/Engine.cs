using System.Diagnostics;
using Deucalion.Monitors;
using Deucalion.Monitors.Events;

namespace Deucalion;

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
                var monitorResponse = args is MonitorResponse mr ? mr : MonitorResponse.DefaultUp;
                UpdatePushMonitorState(pushMonitor, monitorResponse);
            }
        }

        void PushMonitorTimedOut(object? sender, EventArgs _)
        {
            if (sender is PushMonitor pushMonitor)
            {
                UpdatePushMonitorState(pushMonitor, MonitorResponse.DefaultDown);
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

                UpdatePullMonitorState(pullMonitor, response, timerEventAt);
            }
        }

        void UpdatePullMonitorState(PullMonitor pullMonitor, MonitorResponse monitorResponse, DateTimeOffset timerEventAt)
        {
            var name = pullMonitor.Name;
            var at = DateTimeOffset.UtcNow;

            if (catalog.TryGetValue(pullMonitor, out var status))
            {
                // Notify response
                callback(new QueryResponse(name, at, monitorResponse));

                var newState = monitorResponse.State;
                if (status.LastKnownState != MonitorState.Unknown && status.LastKnownState != newState)
                {
                    // State changed

                    // Notify change
                    callback(new StateChanged(name, at, newState));

                    // Update timer interval
                    var dueTime = newState == MonitorState.Up
                        ? pullMonitor.IntervalWhenUpOrDefault
                        : pullMonitor.IntervalWhenDownOrDefault;

                    // Subtract from next dueTime the elapsed time since the current timer event.
                    var deltaUntilNow = DateTimeOffset.UtcNow - timerEventAt;
                    status.QueryTimer?.Change(dueTime - deltaUntilNow, dueTime);
                }

                status.LastKnownState = newState;
                status.LastResponseTime = monitorResponse.ResponseTime ?? TimeSpan.Zero;
            }
        }

        void UpdatePushMonitorState(PushMonitor monitor, MonitorResponse monitorResponse)
        {
            var name = monitor.Name;
            var at = DateTimeOffset.UtcNow;

            if (catalog.TryGetValue(monitor, out var status))
            {
                var newState = monitorResponse.State;

                if (newState == MonitorState.Up)
                {
                    callback(new CheckedIn(name, at, monitorResponse));
                }
                else
                {
                    callback(new CheckInMissed(name, at));
                }

                if (status.LastKnownState != MonitorState.Unknown && status.LastKnownState != newState)
                {
                    callback(new StateChanged(name, at, newState));
                }

                status.LastKnownState = newState;
                status.LastResponseTime = monitorResponse.ResponseTime ?? TimeSpan.Zero;
            }
        }
    }

    internal class MonitorStatus
    {
        internal required MonitorBase Monitor { get; init; }
        internal Timer? QueryTimer { get; set; }
        internal MonitorState LastKnownState { get; set; } = MonitorState.Unknown;
        internal TimeSpan LastResponseTime { get; set; } = TimeSpan.Zero;
    }
}
