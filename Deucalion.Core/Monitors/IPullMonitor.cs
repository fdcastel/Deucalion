using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public interface IPullMonitor<out TOptions> : IMonitor<TOptions> where TOptions : PullMonitorOptions
    {
        public Task<MonitorResponse> QueryAsync();
    }
}
