using System.Diagnostics;
using Deucalion.Monitors;
using Deucalion.Monitors.Events;
using Deucalion.Monitors.Options;
using Deucalion.Options;

namespace Deucalion
{
    public class Engine
    {
        public EngineOptions Options { get; set; } = new();

        public async Task RunAsync(IEnumerable<IMonitor<MonitorOptions>> monitors, Action<MonitorEvent> callback, CancellationToken stopToken)
        {
            var start = DateTime.Now;

            var monitorList = monitors.ToList();
            var lastMonitorStates = new Dictionary<IMonitor<MonitorOptions>, MonitorState>(monitorList.Select(m => KeyValuePair.Create(m, MonitorState.Unknown)));

            while (!stopToken.IsCancellationRequested)
            {
                var tasks = monitorList.Select(async monitor =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    var newState = await monitor.QueryAsync();
                    stopwatch.Stop();

                    var name = monitor.Options.Name;
                    callback(new MonitorResponse(name, DateTime.Now - start, newState, stopwatch.Elapsed));

                    if (lastMonitorStates.TryGetValue(monitor, out var previousState))
                    {
                        if (previousState != MonitorState.Unknown && previousState != newState)
                        {
                            callback(new MonitorChange(name, DateTime.Now - start, newState));
                            lastMonitorStates[monitor] = newState;
                        }
                    }
                }).ToArray();

                try
                {
                    Task.WaitAll(tasks, stopToken);
                }
                catch (OperationCanceledException)
                {
                    // Cancelled. Nothing to do.
                }

                await Task.Delay(Options.IntervalOrDefault, stopToken);
            }
        }
    }
}
