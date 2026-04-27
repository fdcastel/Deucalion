using Deucalion.Application;
using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests;

public class WarnThresholdPolicyTests
{
    private static readonly TimeSpan TypeDefault = TimeSpan.FromSeconds(1);

    [Fact]
    public void NullP95_ReturnsNull()
    {
        var auto = WarnThresholdPolicy.ComputeAuto(p95: null, sampleCount: 100, typeDefault: TypeDefault);
        Assert.Null(auto);
    }

    [Fact]
    public void TooFewSamples_ReturnsNull()
    {
        var auto = WarnThresholdPolicy.ComputeAuto(
            p95: TimeSpan.FromMilliseconds(50),
            sampleCount: PullMonitor.AutoWarnMinSamples - 1,
            typeDefault: TypeDefault);
        Assert.Null(auto);
    }

    [Fact]
    public void NormalP95_AppliesMultiplier()
    {
        var auto = WarnThresholdPolicy.ComputeAuto(
            p95: TimeSpan.FromMilliseconds(50),
            sampleCount: 100,
            typeDefault: TypeDefault);
        Assert.Equal(TimeSpan.FromMilliseconds(50 * PullMonitor.AutoWarnMultiplier), auto);
    }

    [Fact]
    public void TinyP95_ClampsToFloor()
    {
        // P95=0.5ms × 3 = 1.5ms — below the 5ms floor.
        var auto = WarnThresholdPolicy.ComputeAuto(
            p95: TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / 2),
            sampleCount: 100,
            typeDefault: TypeDefault);
        Assert.Equal(PullMonitor.AutoWarnFloor, auto);
    }

    [Fact]
    public void HugeP95_ClampsToTypeDefaultCeiling()
    {
        // 5s × 3 = 15s, but capped by the per-type default.
        var auto = WarnThresholdPolicy.ComputeAuto(
            p95: TimeSpan.FromSeconds(5),
            sampleCount: 100,
            typeDefault: TypeDefault);
        Assert.Equal(TypeDefault, auto);
    }
}
