using System.Net;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Network;

public class DnsMonitorTests
{
    [Fact]
    public async Task DnsMonitor_ReturnsUp_WhenReachable()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com" };
        var result = await dnsMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, result.State);
        Assert.StartsWith("google.com.", result.ResponseText);
    }

    [Fact]
    public async Task DnsMonitor_ReturnsDown_WhenUnreachable()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com.fake", Resolver = IPEndPoint.Parse("1.1.1.1") };
        var result = await dnsMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Non-Existent Domain", result.ResponseText); // Hardcoded in DnsClient
    }

    [Fact]
    public async Task DnsMonitor_ReturnsDown_WhenTimedOut()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com", Resolver = IPEndPoint.Parse("127.0.0.1") };
        var result = await dnsMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, result.State);
        Assert.Contains("timed out", result.ResponseText); // Hardcoded in DnsClient
    }

    [Fact]
    public async Task DnsMonitor_WorksWith_Resolver()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com", Resolver = IPEndPoint.Parse("1.1.1.1") };
        var result = await dnsMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, result.State);

        dnsMonitor = new() { Host = "google.com", Resolver = IPEndPoint.Parse("1.2.3.4:99") };
        result = await dnsMonitor.QueryAsync();
        Assert.Equal(MonitorState.Down, result.State);
    }

    [Fact]
    public async Task DnsMonitor_WorksWith_RecordType()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com", RecordType = DnsClient.QueryType.AAAA };
        var result = await dnsMonitor.QueryAsync();
        Assert.Equal(MonitorState.Up, result.State);
        Assert.Contains("AAAA 2800", result.ResponseText);
    }
}
