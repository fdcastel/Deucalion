using Deucalion.Configuration;

namespace Deucalion.Network.Configuration;

public record CheckInMonitorConfiguration : MonitorConfiguration
{
    public string? Secret { get; set; }
    public TimeSpan? IntervalToDown { get; set; }
}
