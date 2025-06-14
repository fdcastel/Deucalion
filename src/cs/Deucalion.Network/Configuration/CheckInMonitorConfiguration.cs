using Deucalion.Configuration;

namespace Deucalion.Network.Configuration;

public record CheckInMonitorConfiguration : PullMonitorConfiguration
{
    public string? Secret { get; set; }
    public TimeSpan? IntervalToDown { get; set; }
}
