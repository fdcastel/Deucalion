using Deucalion.Monitors;
using Deucalion.Tests.Monitors.Options;

namespace Deucalion.Tests.Monitors
{
    internal class FakeMonitor : IMonitor<FakeMonitorOptions>
    {
        public required FakeMonitorOptions Options { get; init; }

        public async Task<bool> IsUpAsync()
        {
            await Task.Delay(Options.Delay);
            return true;
        }
    }
}
