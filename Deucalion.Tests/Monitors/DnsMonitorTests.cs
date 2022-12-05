using System.Net;
using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class DnsMonitorTests
    {
        [Fact]
        public async Task DnsMonitor_ReturnsTrue_WhenReachable()
        {
            DnsMonitor dnsMonitor = new() { Options = new() { HostName = "google.com" } };
            var result = await dnsMonitor.IsUpAsync();
            Assert.True(result);
        }

        [Fact]
        public async Task DnsMonitor_ReturnsFalse_WhenUnreachable()
        {
            DnsMonitor dnsMonitor = new() { Options = new() { HostName = "google.com.fake" } };
            var result = await dnsMonitor.IsUpAsync();
            Assert.False(result);
        }

        [Fact]
        public async Task DnsMonitor_WorksWith_Resolver()
        {
            DnsMonitor dnsMonitor = new() { Options = new() { HostName = "google.com" } };
            var result = await dnsMonitor.IsUpAsync();
            Assert.True(result);

            dnsMonitor = new() { Options = new() { HostName = "google.com", Resolver = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 99) } };
            result = await dnsMonitor.IsUpAsync();
            Assert.False(result);
        }

        [Fact]
        public async Task DnsMonitor_WorksWith_RecordType()
        {
            DnsMonitor dnsMonitor = new() { Options = new() { HostName = "google.com", RecordType = DnsClient.QueryType.AAAA } };
            var result = await dnsMonitor.IsUpAsync();
            Assert.True(result);
        }
    }
}
