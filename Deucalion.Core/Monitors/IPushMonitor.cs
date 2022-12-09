using Deucalion.Monitors.Options;

namespace Deucalion.Monitors;

public interface IPushMonitor<out TOptions> : IMonitor<TOptions> where TOptions : PushMonitorOptions
{
    public void CheckIn(MonitorResponse? response = null);
}
