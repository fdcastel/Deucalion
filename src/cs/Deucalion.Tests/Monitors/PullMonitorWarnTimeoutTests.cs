using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors;

public class PullMonitorWarnTimeoutTests
{
    [Fact]
    public void DnsMonitor_NoConfig_FallsBackToTypeDefault()
    {
        var monitor = new DnsMonitor { Host = "example.com" };
        Assert.Null(monitor.WarnTimeout);
        Assert.Null(monitor.AutoWarnTimeout);
        Assert.Equal(DnsMonitor.DefaultDnsWarnTimeout, monitor.EffectiveWarnTimeout);
    }

    [Fact]
    public void DnsMonitor_AutoSet_UsesAutoOverTypeDefault()
    {
        var monitor = new DnsMonitor
        {
            Host = "example.com",
            AutoWarnTimeout = TimeSpan.FromMilliseconds(20),
        };
        Assert.Equal(TimeSpan.FromMilliseconds(20), monitor.EffectiveWarnTimeout);
    }

    [Fact]
    public void DnsMonitor_ExplicitWarnTimeout_OverridesAuto()
    {
        var monitor = new DnsMonitor
        {
            Host = "example.com",
            WarnTimeout = TimeSpan.FromMilliseconds(123),
            AutoWarnTimeout = TimeSpan.FromMilliseconds(20),
        };
        Assert.Equal(TimeSpan.FromMilliseconds(123), monitor.EffectiveWarnTimeout);
    }

    [Fact]
    public void HttpMonitor_NoConfig_FallsBackToBaseDefault()
    {
        var monitor = new HttpMonitor { Url = new Uri("https://example.com") };
        Assert.Null(monitor.WarnTimeout);
        Assert.Equal(PullMonitor.DefaultWarnTimeout, monitor.EffectiveWarnTimeout);
    }

    [Fact]
    public void PingMonitor_TypeDefaultMatchesPingDefault()
    {
        var monitor = new PingMonitor { Host = "example.com" };
        Assert.Equal(PingMonitor.DefaultPingWarnTimeout, monitor.TypeDefaultWarnTimeout);
        Assert.Equal(PingMonitor.DefaultPingWarnTimeout, monitor.EffectiveWarnTimeout);
    }
}
