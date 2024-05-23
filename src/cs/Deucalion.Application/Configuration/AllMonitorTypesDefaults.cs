namespace Deucalion.Application.Configuration;

public record AllMonitorTypesDefaults : MonitorTypeDefaults
{
    public int? IgnoreFailCount { get; set; }
    public bool? UpsideDown { get; set; }

    public MonitorTypeDefaults? Dns { get; set; }
    public MonitorTypeDefaults? Http { get; set; }
    public MonitorTypeDefaults? Ping { get; set; }
    public MonitorTypeDefaults? Tcp { get; set; }
}
