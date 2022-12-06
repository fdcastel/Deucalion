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
                var period = monitor is CheckInMonitor checkInMonitor
                    ? checkInMonitor.Options.IntervalToDownOrDefault
                    : monitor.Options.IntervalWhenUpOrDefault;

                status.QueryTimer = new Timer(QueryMonitor, monitor, TimeSpan.Zero, period);
            }

            stopToken.WaitHandle.WaitOne();

            async void QueryMonitor(object? mon)
            {
                if (mon is IMonitor<MonitorOptions> monitor)
                {
                    var name = monitor.Options.Name;

                    MonitorState newState;
                    TimeSpan elapsed;
                    if (monitor is IPushMonitor<MonitorOptions> pushMonitor)
                    {
                        newState = await pushMonitor.QueryAsync();
                        elapsed = TimeSpan.Zero;
                    }
                    else if (monitor is IPullMonitor<MonitorOptions> pullMonitor)
                    {
                        var stopwatch = Stopwatch.StartNew();
                        newState = await pullMonitor.QueryAsync();
                        stopwatch.Stop();
                        elapsed = stopwatch.Elapsed;
                    }
                    else
                    {
                        throw new Exception("Unknown monitor type");
                    }

                    if (catalog.TryGetValue(monitor, out var status))
                    {
                        if (status.LastState != MonitorState.Unknown && status.LastState != newState)
                        {
                            callback(new MonitorChange(name, DateTime.Now - start, newState));
                        }
                        status.LastState = newState;
                    }

                    callback(new MonitorResponse(name, DateTime.Now - start, newState, elapsed));
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
