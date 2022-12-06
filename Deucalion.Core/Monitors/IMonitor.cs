using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public interface IMonitor<out TOptions> where TOptions : MonitorOptions
    {
        public TOptions Options { get; }
    }
}
