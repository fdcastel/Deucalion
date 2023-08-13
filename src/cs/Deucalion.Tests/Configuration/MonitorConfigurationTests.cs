using System.Net;
using Deucalion.Application.Configuration;
using Deucalion.Monitors;
using Deucalion.Network.Monitors;
using DnsClient;
using Xunit;

namespace Deucalion.Tests.Configuration;

public class MonitorConfigurationTests
{
    [Fact]
    public void EmptyConfiguration_Throws()
    {
        var exception = CatchConfigurationException(string.Empty);
        Assert.Equal(MonitorConfiguration.Messages.ConfigurationMustNotBeEmpty, exception.Message);
    }

    [Fact]
    public void EmptyMonitor_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              m1:
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(MonitorConfiguration.Messages.ConfigurationMonitorCannotBeEmpty, "m1"), exception.Message);
    }

    [Fact]
    public void MonitorDns_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mdns:
                !dns
                host: google.com
                recordType: A
                resolver: 1.1.1.1
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var dnsMonitor = Assert.IsType<DnsMonitor>(monitor);
        Assert.Equal("mdns", dnsMonitor.Name);
        Assert.Equal("google.com", dnsMonitor.Host);
        Assert.Equal(QueryType.A, dnsMonitor.RecordType);
        Assert.Equal(IPEndPoint.Parse("1.1.1.1:53"), dnsMonitor.Resolver);
    }

    [Fact]
    public void MonitorHttp_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mhttp:
                !http
                url: http://github.com/api
                expectedStatusCode: 202
                expectedResponseBodyPattern: .*
                ignoreCertificateErrors: true
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var httpMonitor = Assert.IsType<HttpMonitor>(monitor);
        Assert.Equal("mhttp", httpMonitor.Name);
        Assert.Equal(new Uri("http://github.com/api"), httpMonitor.Url);
        Assert.Equal(HttpStatusCode.Accepted, httpMonitor.ExpectedStatusCode);
        Assert.Equal(".*", httpMonitor.ExpectedResponseBodyPattern);
        Assert.Equal(true, httpMonitor.IgnoreCertificateErrors);
    }

    [Fact]
    public void MonitorPing_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mping:
                !ping
                host: 192.168.1.1
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var pingMonitor = Assert.IsType<PingMonitor>(monitor);
        Assert.Equal("mping", pingMonitor.Name);
        Assert.Equal("192.168.1.1", pingMonitor.Host);
    }

    [Fact]
    public void MonitorTcp_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mtcp:
                !tcp
                host: 192.168.1.2
                port: 65000
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var tcpMonitor = Assert.IsType<TcpMonitor>(monitor);
        Assert.Equal("mtcp", tcpMonitor.Name);
        Assert.Equal("192.168.1.2", tcpMonitor.Host);
        Assert.Equal(65000, tcpMonitor.Port);
    }

    private static ConfigurationErrorException CatchConfigurationException(string configuration) =>
        Assert.Throws<ConfigurationErrorException>(() =>
        {
            using var reader = new StringReader(configuration);
            MonitorConfiguration.ReadFromStream(reader);
        });

    private static MonitorConfiguration ReadConfiguration(string configuration)
    {
        using var reader = new StringReader(configuration);
        return MonitorConfiguration.ReadFromStream(reader);
    }

    private static MonitorBase ReadSingleMonitorFromConfiguration(string ConfigurationContent)
    {
        var configuration = ReadConfiguration(ConfigurationContent);
        Assert.Single(configuration.Monitors);

        var monitor = configuration.Monitors.First().Value;
        Assert.NotNull(monitor);

        return monitor;
    }
}
