using System.Diagnostics;
using Deucalion.Monitors;
using Deucalion.Monitors.Options;
using Deucalion.Options;

namespace Deucalion
{
    public class Engine
    {
        public EngineOptions Options { get; set; } = new();

        public async Task RunAsync(IEnumerable<IMonitor<MonitorOptions>> monitors, Action<MonitorResponse> callback, CancellationToken stopToken)
        {
            while (!stopToken.IsCancellationRequested)
            {
                var tasks = monitors.Select(async monitor =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    var isUp = await monitor.IsUpAsync();
                    stopwatch.Stop();

                    var name = monitor.Options.Name;
                    callback(new MonitorResponse(name, isUp, stopwatch.Elapsed));
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
