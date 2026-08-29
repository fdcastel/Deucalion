using Deucalion.Application;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
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

    // Enough history for the auto baseline to kick in: P95=50ms x 3 = 150ms.
    private static readonly TimeSpan P95 = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ExpectedAuto = TimeSpan.FromMilliseconds(50 * PullMonitor.AutoWarnMultiplier);
    private const int EnoughSamples = 100;

    [Fact]
    public void Compute_ReportsTheAutoThreshold_WithoutWritingTheMonitor()
    {
        // Issue #15: GET /api/monitors runs on request threads and must not mutate the live
        // monitor. The engine's own baseline (set here to a distinct value) has to survive.
        var engineBaseline = TimeSpan.FromMilliseconds(999);
        var monitor = new HttpMonitor { Url = new Uri("https://example.com"), AutoWarnTimeout = engineBaseline };

        var (effectiveWarn, timeout) = WarnThresholdPolicy.Compute(monitor, P95, EnoughSamples);

        Assert.Equal(ExpectedAuto, effectiveWarn);
        Assert.Equal(monitor.Timeout, timeout);
        Assert.Equal(engineBaseline, monitor.AutoWarnTimeout);
    }

    [Fact]
    public void Compute_ExplicitWarnTimeoutWins()
    {
        var monitor = new HttpMonitor { Url = new Uri("https://example.com"), WarnTimeout = TimeSpan.FromMilliseconds(123) };

        var (effectiveWarn, _) = WarnThresholdPolicy.Compute(monitor, P95, EnoughSamples);

        Assert.Equal(TimeSpan.FromMilliseconds(123), effectiveWarn);
        Assert.Null(monitor.AutoWarnTimeout);
    }

    [Fact]
    public void Compute_TooFewSamples_FallsBackToTypeDefault()
    {
        var monitor = new DnsMonitor { Host = "example.com" };

        var (effectiveWarn, _) = WarnThresholdPolicy.Compute(monitor, P95, PullMonitor.AutoWarnMinSamples - 1);

        Assert.Equal(DnsMonitor.DefaultDnsWarnTimeout, effectiveWarn);
        Assert.Null(monitor.AutoWarnTimeout);
    }

    [Fact]
    public void Compute_UnknownMonitor_ReturnsNulls()
    {
        Assert.Equal((null, null), WarnThresholdPolicy.Compute(null, P95, EnoughSamples));
    }

    [Fact]
    public void Refresh_WritesTheAutoThreshold_AndReportsTheSameValuesAsCompute()
    {
        var monitor = new HttpMonitor { Url = new Uri("https://example.com") };
        Assert.Null(monitor.AutoWarnTimeout);

        var computed = WarnThresholdPolicy.Compute(monitor, P95, EnoughSamples);
        var refreshed = WarnThresholdPolicy.Refresh(monitor, P95, EnoughSamples);

        Assert.Equal(ExpectedAuto, monitor.AutoWarnTimeout);
        Assert.Equal(computed, refreshed);
        Assert.Equal(ExpectedAuto, monitor.EffectiveWarnTimeout);
    }
}
