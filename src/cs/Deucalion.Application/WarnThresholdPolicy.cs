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
}
