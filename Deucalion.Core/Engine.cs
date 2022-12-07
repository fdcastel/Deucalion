using System.Diagnostics;
using Deucalion.Monitors;
using Deucalion.Monitors.Events;
using Deucalion.Monitors.Options;

namespace Deucalion
{
    public class Engine
    {
        public void Run(IEnumerable<IMonitor<MonitorOptions>> monitors, Action<MonitorEvent> callback, CancellationToken stopToken)
        {
            var start = DateTime.Now;

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
                else if (monitor is IPullMonitor<PullMonitorOptions> pullMonitor)
                {
                    status.QueryTimer = new Timer(QueryPullMonitor, monitor, TimeSpan.Zero, pullMonitor.Options.IntervalWhenUpOrDefault);
                }
            }

            try
            {
                stopToken.WaitHandle.WaitOne();
            }
            finally
            {
                foreach (var (_, status) in catalog)
                    status.QueryTimer?.Dispose();
            }

            void PushMonitorCheckedIn(object? sender, EventArgs _)
            {
                if (sender is IPushMonitor<PushMonitorOptions> pushMonitor)
                    UpdatePushMonitorState(pushMonitor, MonitorState.Up);
            }

            void PushMonitorTimedOut(object? sender, EventArgs _)
            {
                if (sender is IPushMonitor<PushMonitorOptions> pushMonitor)
                    UpdatePushMonitorState(pushMonitor, MonitorState.Down);
            }

            async void QueryPullMonitor(object? sender)
            {
                var timerEventAt = DateTime.Now;

                if (sender is IPullMonitor<PullMonitorOptions> pullMonitor)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var newState = await pullMonitor.QueryAsync();
                    stopwatch.Stop();

                    var queryDuration = stopwatch.Elapsed;
                    UpdatePullMonitorState(pullMonitor, newState, queryDuration, timerEventAt);
                }
            }

            void UpdatePullMonitorState(IPullMonitor<PullMonitorOptions> pullMonitor, MonitorState newState, TimeSpan queryDuration, DateTime timerEventAt)
            {
                var name = pullMonitor.Options.Name;
                var at = DateTime.Now - start;

                if (catalog.TryGetValue(pullMonitor, out var status))
                {
                    // Notify response
                    callback(new QueryResponse(name, at, newState, queryDuration));

                    if (status.LastKnownState != MonitorState.Unknown && status.LastKnownState != newState)
                    {
                        // State changed

                        // Notify change
                        callback(new StateChanged(name, at, newState));

                        // Update timer interval
                        var dueTime = newState == MonitorState.Up
                            ? pullMonitor.Options.IntervalWhenUpOrDefault
                            : pullMonitor.Options.IntervalWhenDownOrDefault;

                        // Subtract from next dueTime the already elapsed time since the current timer event.
                        var deltaUntilNow = DateTime.Now - timerEventAt;
                        status.QueryTimer?.Change(dueTime - deltaUntilNow, dueTime);
                    }

                    status.LastKnownState = newState;
                }
            }

            void UpdatePushMonitorState(IMonitor<MonitorOptions> monitor, MonitorState newState)
            {
                var name = monitor.Options.Name;
                var at = DateTime.Now - start;

                if (catalog.TryGetValue(monitor, out var status))
                {
                    if (newState == MonitorState.Up)
                        callback(new CheckedIn(name, at));
                    else
                        callback(new CheckInMissed(name, at));

                    if (status.LastKnownState != MonitorState.Unknown && status.LastKnownState != newState)
                        callback(new StateChanged(name, at, newState));

                    status.LastKnownState = newState;
                }
            }
        }

        internal class MonitorStatus
        {
            internal required IMonitor<MonitorOptions> Monitor { get; init; }
            internal Timer? QueryTimer { get; set; }
            internal MonitorState LastKnownState { get; set; } = MonitorState.Unknown;
        }
    }
}
