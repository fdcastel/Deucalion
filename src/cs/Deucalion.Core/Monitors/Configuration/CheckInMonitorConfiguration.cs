namespace Deucalion.Monitors.Configuration;

public record CheckInMonitorConfiguration : PushMonitorConfiguration
{
    public string? Secret { get; set; }
}
