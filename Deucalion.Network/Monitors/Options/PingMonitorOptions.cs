using Deucalion.Monitors.Options;

namespace Deucalion.Network.Monitors.Options;

public class PingMonitorOptions : PullMonitorOptions
{
    public string Host { get; set; } = default!;
}
