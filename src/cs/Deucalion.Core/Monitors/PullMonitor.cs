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

    // Rolling window (in probes) for the auto-WARN baseline and the reported availability.
    // Documented in README.md ("the last 60 successful probes"); every stats call must use it,
    // or the numbers jump between the initial snapshot and live SSE updates.
    public const int StatsWindow = 60;

    public string Name { get; set; } = string.Empty;

    public int IgnoreFailCount { get; set; }
    public bool UpsideDown { get; set; }

    public TimeSpan IntervalWhenUp { get; set; } = DefaultIntervalWhenUp;
    public TimeSpan IntervalWhenDown { get; set; } = DefaultIntervalWhenDown;

    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    // null means "use auto"; populated when YAML (or the per-type defaults block) sets it explicitly.
    public TimeSpan? WarnTimeout { get; set; }

    // Computed from rolling P95; null until enough samples accumulate.
    //
    // Written by the engine's event consumer and read by this monitor's own polling loop, on
    // different threads. A TimeSpan? is a 16-byte struct, so a plain field can be read half
    // written (HasValue from one write, Ticks from another -- issue #15). Storing it behind a
    // volatile reference to an immutable boxed TimeSpan makes every read and write atomic.
    private volatile object? _autoWarnTimeout;

    public TimeSpan? AutoWarnTimeout
    {
        get => _autoWarnTimeout is TimeSpan value ? value : null;
        set => _autoWarnTimeout = value;
    }

    // Per-type fallback when neither manual nor auto is available. Subclasses override.
    public virtual TimeSpan TypeDefaultWarnTimeout => DefaultWarnTimeout;

    public TimeSpan EffectiveWarnTimeout =>
        WarnTimeout ?? AutoWarnTimeout ?? TypeDefaultWarnTimeout;

    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    public abstract Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default);
}
