using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public interface IMonitor<out T> where T : MonitorOptions
    {
        public T Options { get; }

        public Task<bool> IsUpAsync();
    }
}
