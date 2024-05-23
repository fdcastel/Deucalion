namespace Deucalion.Monitors.Configuration;

public record PushMonitorConfiguration : MonitorConfiguration
{
    public TimeSpan? IntervalToDown { get; set; }
}
