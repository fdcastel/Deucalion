using Deucalion.Monitors;

namespace Deucalion.Application;

public static class WarnThresholdPolicy
{
    public static TimeSpan? ComputeAuto(TimeSpan? p95, int sampleCount, TimeSpan typeDefault)
    {
        if (p95 is null) return null;
        if (sampleCount < PullMonitor.AutoWarnMinSamples) return null;

        var raw = TimeSpan.FromTicks(p95.Value.Ticks * PullMonitor.AutoWarnMultiplier);
        if (raw < PullMonitor.AutoWarnFloor) raw = PullMonitor.AutoWarnFloor;
        if (raw > typeDefault) raw = typeDefault;
        return raw;
    }

    /// <summary>
    /// Computes the thresholds a monitor would report for the given rolling history, without
    /// touching the monitor. This is the read-only path for request handlers (issue #15: a
    /// <c>GET /api/monitors</c> must not reset the auto-WARN baseline of the live engine).
    /// </summary>
    /// <returns>
    /// The effective WARN threshold and the hard timeout, or (null, null) when the monitor is
    /// unknown -- e.g. a configuration entry with no corresponding live monitor.
    /// </returns>
    public static (TimeSpan? EffectiveWarn, TimeSpan? Timeout) Compute(PullMonitor? monitor, TimeSpan? p95, int sampleCount)
    {
        if (monitor is null)
        {
            return (null, null);
        }

        var auto = ComputeAuto(p95, sampleCount, monitor.TypeDefaultWarnTimeout);
        return (monitor.WarnTimeout ?? auto ?? monitor.TypeDefaultWarnTimeout, monitor.Timeout);
    }

    /// <summary>
    /// Refreshes a monitor's auto-WARN baseline from its current rolling history and returns the
    /// thresholds to report. The new value persists in-process until the next probe, so subsequent
    /// checks pick it up immediately. Only the engine's event consumer may call this; every other
    /// caller uses <see cref="Compute"/>.
    /// </summary>
    /// <returns>
    /// The effective WARN threshold and the hard timeout, or (null, null) when the monitor is
    /// unknown -- e.g. a configuration entry with no corresponding live monitor.
    /// </returns>
    public static (TimeSpan? EffectiveWarn, TimeSpan? Timeout) Refresh(PullMonitor? monitor, TimeSpan? p95, int sampleCount)
    {
        if (monitor is null)
        {
            return (null, null);
        }

        monitor.AutoWarnTimeout = ComputeAuto(p95, sampleCount, monitor.TypeDefaultWarnTimeout);
        return (monitor.EffectiveWarnTimeout, monitor.Timeout);
    }
}
