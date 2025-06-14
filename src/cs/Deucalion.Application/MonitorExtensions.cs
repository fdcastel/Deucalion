using System.Threading.Channels;
using Deucalion.Events;
using Deucalion.Monitors;

namespace Deucalion.Application;

public static class MonitorExtensions
{
    public static async Task RunAllAsync(this IEnumerable<PullMonitor> monitors, ChannelWriter<IMonitorEvent> writer, CancellationToken stopToken)
    {
        var monitorTasks = new List<Task>();

        foreach (var monitor in monitors)
        {
            if (monitor is PullMonitor pullMonitor)
            {
                monitorTasks.Add(pullMonitor.RunMonitorLoopAsync(writer, stopToken));
            }
        }

        try
        {
            await Task.WhenAll(monitorTasks);
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
}
