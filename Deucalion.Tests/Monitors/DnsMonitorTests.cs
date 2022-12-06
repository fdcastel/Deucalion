﻿using System.Net;
using Deucalion.Monitors;
using Xunit;

namespace Deucalion.Tests.Monitors
{
    public class DnsMonitorTests
    {
        [Fact]
        public async Task DnsMonitor_ReturnsUp_WhenReachable()
        {
            DnsMonitor dnsMonitor = new() { Options = new() { HostName = "google.com" } };
            var result = await dnsMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);
        }

        [Fact]
        public async Task DnsMonitor_ReturnsDown_WhenUnreachable()
        {
            DnsMonitor dnsMonitor = new() { Options = new() { HostName = "google.com.fake" } };
            var result = await dnsMonitor.QueryAsync();
            Assert.Equal(MonitorState.Down, result);
        }

        [Fact]
        public async Task DnsMonitor_WorksWith_Resolver()
        {
            DnsMonitor dnsMonitor = new() { Options = new() { HostName = "google.com" } };
            var result = await dnsMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);

            dnsMonitor = new() { Options = new() { HostName = "google.com", Resolver = new IPEndPoint(IPAddress.Parse("1.2.3.4"), 99) } };
            result = await dnsMonitor.QueryAsync();
            Assert.Equal(MonitorState.Down, result);
        }

        [Fact]
        public async Task DnsMonitor_WorksWith_RecordType()
        {
            DnsMonitor dnsMonitor = new() { Options = new() { HostName = "google.com", RecordType = DnsClient.QueryType.AAAA } };
            var result = await dnsMonitor.QueryAsync();
            Assert.Equal(MonitorState.Up, result);
        }
    }
}
