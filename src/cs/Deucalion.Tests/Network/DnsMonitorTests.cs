using System.Net;
using System.Net.Sockets;
using Deucalion.Network.Monitors;
using Xunit;

namespace Deucalion.Tests.Network;

/// <summary>
/// Hermetic tests point the monitor at a loopback UDP socket. Real queries against public
/// resolvers are skipped unless DEUCALION_TESTS_NETWORK=1.
/// </summary>
public class DnsMonitorTests
{
    private const string NoCustomDnsPort = "System.Net.DnsResolver only queries port 53 on Windows.";

    [Fact(Skip = NoCustomDnsPort, SkipUnless = nameof(TestEnvironment.CustomDnsPortSupported), SkipType = typeof(TestEnvironment))]
    public async Task ReturnsDown_WithTimeoutText_WhenTheResolverNeverAnswers()
    {
        // Bound but never read: the query is accepted by the socket and goes unanswered.
        using var silentResolver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        DnsMonitor dnsMonitor = new()
        {
            Host = "example.com",
            Resolver = (IPEndPoint)silentResolver.Client.LocalEndPoint!,
            Timeout = TimeSpan.FromMilliseconds(200),
        };

        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Timeout", result.ResponseText);
    }

    [Fact(Skip = NoCustomDnsPort, SkipUnless = nameof(TestEnvironment.CustomDnsPortSupported), SkipType = typeof(TestEnvironment))]
    public async Task ReturnsDown_WhenNothingIsListening()
    {
        // Bind to claim a free port, then release it so the query is refused.
        int port;
        using (var claim = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            port = ((IPEndPoint)claim.Client.LocalEndPoint!).Port;
        }

        DnsMonitor dnsMonitor = new()
        {
            Host = "example.com",
            Resolver = new IPEndPoint(IPAddress.Loopback, port),
            Timeout = TimeSpan.FromSeconds(2),
        };

        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MonitorState.Down, result.State);
        Assert.NotNull(result.ResponseText);
    }

    [Fact(Skip = "Only Windows rejects custom DNS ports.", SkipWhen = nameof(TestEnvironment.CustomDnsPortSupported), SkipType = typeof(TestEnvironment))]
    public void Resolver_RejectsCustomPort_OnWindows()
    {
        DnsMonitor dnsMonitor = new() { Host = "example.com" };

        Assert.Throws<PlatformNotSupportedException>(() => dnsMonitor.Resolver = IPEndPoint.Parse("1.1.1.1:5353"));

        // Port 53, explicit or implied, stays accepted.
        dnsMonitor.Resolver = IPEndPoint.Parse("1.1.1.1");
        Assert.Equal(IPEndPoint.Parse("1.1.1.1:53"), dnsMonitor.Resolver);
    }

    [Theory]
    [InlineData(DnsResponseCode.NxDomain, "Non-Existent Domain")]
    [InlineData(DnsResponseCode.ServerFailure, "Server Failure")]
    [InlineData(DnsResponseCode.Refused, "Query Refused")]
    [InlineData((DnsResponseCode)9, "DNS error 9")]
    public void Describe_NamesTheResponseCode(DnsResponseCode responseCode, string expected) =>
        Assert.Equal(expected, DnsMonitor.Describe(responseCode));

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task DnsMonitor_ReturnsUp_WhenReachable()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com" };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Up, result.State);
        Assert.StartsWith("google.com. ", result.ResponseText);
        Assert.Contains(" IN A ", result.ResponseText);
    }

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task DnsMonitor_ReturnsDown_WhenTheDomainDoesNotExist()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com.fake", Resolver = IPEndPoint.Parse("1.1.1.1") };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Down, result.State);
        Assert.Equal("Non-Existent Domain", result.ResponseText);
    }

    [Fact(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    public async Task DnsMonitor_WorksWith_Resolver()
    {
        DnsMonitor dnsMonitor = new() { Host = "google.com", Resolver = IPEndPoint.Parse("1.1.1.1") };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Up, result.State);

        // Nothing listens on loopback :53, so the query fails on every platform. Not an
        // unroutable public address: routers that intercept outbound :53 would answer it.
        dnsMonitor = new() { Host = "google.com", Resolver = IPEndPoint.Parse("127.0.0.1") };
        result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Down, result.State);
    }

    [Theory(Skip = "Requires public internet access. Set DEUCALION_TESTS_NETWORK=1 to run.", SkipUnless = nameof(TestEnvironment.NetworkTestsEnabled), SkipType = typeof(TestEnvironment))]
    [InlineData(DnsRecordType.A, "google.com")]
    [InlineData(DnsRecordType.AAAA, "google.com")]
    [InlineData(DnsRecordType.CNAME, "www.github.com")]
    [InlineData(DnsRecordType.MX, "google.com")]
    [InlineData(DnsRecordType.NS, "google.com")]
    [InlineData(DnsRecordType.PTR, "1.1.1.1")]
    [InlineData(DnsRecordType.SRV, "_imaps._tcp.gmail.com")]
    [InlineData(DnsRecordType.TXT, "google.com")]
    public async Task DnsMonitor_WorksWith_RecordType(DnsRecordType recordType, string host)
    {
        DnsMonitor dnsMonitor = new() { Host = host, RecordType = recordType, Resolver = IPEndPoint.Parse("1.1.1.1") };
        var result = await dnsMonitor.QueryAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MonitorState.Up, result.State);
        // Do not assert the record data: answers depend on the resolver's geography.
        Assert.Contains($" IN {recordType} ", result.ResponseText);
    }
}
