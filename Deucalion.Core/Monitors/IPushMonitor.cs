using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public interface IPushMonitor<out TOptions> : IMonitor<TOptions> where TOptions : MonitorOptions
    {
        public void CheckIn();
    }
}
