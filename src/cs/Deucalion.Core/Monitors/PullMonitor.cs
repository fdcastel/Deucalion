namespace Deucalion.Monitors;

public abstract class PullMonitor : MonitorBase
{
    public static readonly TimeSpan DefaultIntervalWhenUp = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultIntervalWhenDown = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan DefaultWarnTimeout = TimeSpan.FromSeconds(1);


    public TimeSpan? IntervalWhenUp { get; set; }
    public TimeSpan? IntervalWhenDown { get; set; }

    public TimeSpan? Timeout { get; set; }
    public TimeSpan? WarnTimeout { get; set; }

    public abstract Task<MonitorResponse> QueryAsync();
}
