namespace Deucalion.Monitors;

public abstract class PullMonitor
{
    public static readonly TimeSpan DefaultIntervalWhenUp = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultIntervalWhenDown = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan DefaultWarnTimeout = TimeSpan.FromSeconds(1);

    // Auto-WARN tuning. Auto threshold = clamp(P95 * Multiplier, Floor, TypeDefaultWarnTimeout).
    public const int AutoWarnMultiplier = 3;
    public static readonly TimeSpan AutoWarnFloor = TimeSpan.FromMilliseconds(5);
    public const int AutoWarnMinSamples = 20;

    public string Name { get; set; } = string.Empty;

    public int IgnoreFailCount { get; set; }
    public bool UpsideDown { get; set; }

    public TimeSpan IntervalWhenUp { get; set; } = DefaultIntervalWhenUp;
    public TimeSpan IntervalWhenDown { get; set; } = DefaultIntervalWhenDown;

    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    // null means "use auto"; populated when YAML (or the per-type defaults block) sets it explicitly.
    public TimeSpan? WarnTimeout { get; set; }

    // Computed from rolling P95; null until enough samples accumulate.
    public TimeSpan? AutoWarnTimeout { get; set; }

    // Per-type fallback when neither manual nor auto is available. Subclasses override.
    public virtual TimeSpan TypeDefaultWarnTimeout => DefaultWarnTimeout;

    public TimeSpan EffectiveWarnTimeout =>
        WarnTimeout ?? AutoWarnTimeout ?? TypeDefaultWarnTimeout;

    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    public abstract Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default);
}
