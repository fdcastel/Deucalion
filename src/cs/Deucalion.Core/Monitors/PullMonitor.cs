namespace Deucalion.Monitors;

public abstract class PullMonitor
{
    public static readonly TimeSpan DefaultIntervalWhenUp = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultIntervalWhenDown = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan DefaultWarnTimeout = TimeSpan.FromSeconds(1);

    public string Name { get; set; } = string.Empty;

    public int IgnoreFailCount { get; set; }
    public bool UpsideDown { get; set; }

    public TimeSpan IntervalWhenUp { get; set; } = DefaultIntervalWhenUp;
    public TimeSpan IntervalWhenDown { get; set; } = DefaultIntervalWhenDown;

    public TimeSpan Timeout { get; set; } = DefaultTimeout;
    public TimeSpan WarnTimeout { get; set; } = DefaultWarnTimeout;

    public abstract Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default);
}
