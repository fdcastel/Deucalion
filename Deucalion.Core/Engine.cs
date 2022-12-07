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

            stopToken.WaitHandle.WaitOne();

            void PushMonitorCheckedIn(object? sender, EventArgs _)
            {
                if (sender is IPushMonitor<PushMonitorOptions> monitor)
                    UpdateMonitorState(monitor, MonitorState.Up);
            }

            void PushMonitorTimedOut(object? sender, EventArgs _)
            {
                if (sender is IPushMonitor<PushMonitorOptions> monitor)
                    UpdateMonitorState(monitor, MonitorState.Down);
            }

            async void QueryPullMonitor(object? sender)
            {
                if (sender is IPullMonitor<PullMonitorOptions> monitor)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var newState = await monitor.QueryAsync();
                    stopwatch.Stop();

                    var queryDuration = stopwatch.Elapsed;
                    UpdateMonitorState(monitor, newState, queryDuration);
                }
            }

            void UpdateMonitorState(IMonitor<MonitorOptions> monitor, MonitorState newState, TimeSpan queryDuration = default)
            {
                var name = monitor.Options.Name;
                var at = DateTime.Now - start;

                if (monitor is IPullMonitor<PullMonitorOptions>)
                    callback(new QueryResponse(name, at, newState, queryDuration));
                else
                {
                    if (newState == MonitorState.Up)
                        callback(new CheckedIn(name, at));
                    else
                        callback(new CheckInMissed(name, at));
                }

                if (catalog.TryGetValue(monitor, out var status))
                {
                    if (status.LastState != MonitorState.Unknown && status.LastState != newState)
                        callback(new StateChanged(name, at, newState));

                    status.LastState = newState;
                }
            }
        }

        internal class MonitorStatus
        {
            internal required IMonitor<MonitorOptions> Monitor { get; init; }
            internal Timer QueryTimer { get; set; } = default!;

            internal MonitorState LastState { get; set; } = MonitorState.Unknown;
        }
    }
}
