namespace Deucalion.Application.Configuration;

public record MonitorTypeDefaults
{
    public TimeSpan? IntervalWhenDown { get; set; }
    public TimeSpan? IntervalWhenUp { get; set; }

    public TimeSpan? Timeout { get; set; }
    public TimeSpan? WarnTimeout { get; set; }
}
