using Deucalion.Monitors.Options;

namespace Deucalion.Tests.Monitors.Options
{
    public class FakeMonitorOptions : MonitorOptions
    {
        public TimeSpan Delay { get; set; }
    }
}
