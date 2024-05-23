namespace Deucalion.Monitors.Configuration;

public record PullMonitorConfiguration : MonitorConfiguration
{
    public TimeSpan? IntervalWhenUp { get; set; }
    public TimeSpan? IntervalWhenDown { get; set; }

    public TimeSpan? Timeout { get; set; }
    public TimeSpan? WarnTimeout { get; set; }
}
