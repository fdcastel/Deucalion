namespace Deucalion.Configuration;

public record PushMonitorConfiguration : MonitorConfiguration
{
    public TimeSpan? IntervalToDown { get; set; }
}
