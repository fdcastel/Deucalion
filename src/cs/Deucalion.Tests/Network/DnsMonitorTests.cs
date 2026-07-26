using System.Net;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Network;

/// <summary>
/// Real DNS queries against public resolvers. Skipped unless DEUCALION_TESTS_NETWORK=1.
/// </summary>
public class DnsMonitorTests
{
    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task DnsMonitor_ReturnsUp_WhenReachable()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com" };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Up, result.State);
        Assert.StartsWith("google.com.", result.ResponseText);
    }

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task DnsMonitor_ReturnsDown_WhenUnreachable()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com.fake", Resolver = IPEndPoint.Parse("1.1.1.1") };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Non-Existent Domain", result.ResponseText); // Hardcoded in DnsClient
    }

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task DnsMonitor_ReturnsDown_WhenTimedOut()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com", Resolver = IPEndPoint.Parse("127.0.0.1") };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Down, result.State);
        Assert.Contains("timed out", result.ResponseText); // Hardcoded in DnsClient
    }

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task DnsMonitor_WorksWith_Resolver()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com", Resolver = IPEndPoint.Parse("1.1.1.1") };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Up, result.State);

        dnsMonitor = new() { Host = "google.com", Resolver = IPEndPoint.Parse("1.2.3.4:99") };
        result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Down, result.State);
    }

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task DnsMonitor_WorksWith_RecordType()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com", RecordType = DnsClient.QueryType.AAAA };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Up, result.State);
        // Do not assert a specific prefix: the address returned depends on the resolver's
        // geography (the old "AAAA 2800" check only passed near a LACNIC resolver).
        Assert.Contains("AAAA", result.ResponseText);
    }
}
