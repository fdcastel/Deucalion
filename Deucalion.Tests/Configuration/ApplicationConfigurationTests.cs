using System.Net;
using Deucalion.Application.Configuration;
using Deucalion.Monitors;
using Deucalion.Monitors.Options;
using Deucalion.Network.Monitors;
using DnsClient;
using Xunit;

namespace Deucalion.Tests.Configuration;

public class ApplicationConfigurationTests
{
    [Fact]
    public void EmptyConfiguration_Throws()
    {
        var exception = CatchConfigurationException(string.Empty);
        Assert.Equal(ApplicationConfiguration.Messages.ConfigurationMustNotBeEmpty, exception.Message);
    }

    [Fact]
    public void EmptyMonitor_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              m1:
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationMonitorCannotBeEmpty, "m1"), exception.Message);
    }

    [Fact]
    public void MonitorDns_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mdns:
                !dns
                options:
                  host: google.com
                  recordType: A
                  resolver: 8.8.8.8
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var dnsMonitor = Assert.IsType<DnsMonitor>(monitor);
        Assert.Equal("mdns", dnsMonitor.Options.Name);
        Assert.Equal("google.com", dnsMonitor.Options.Host);
        Assert.Equal(QueryType.A, dnsMonitor.Options.RecordType);
        Assert.Equal(IPEndPoint.Parse("8.8.8.8:0"), dnsMonitor.Options.Resolver);
    }

    [Fact]
    public void MonitorHttp_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mhttp:
                !http
                options:
                  url: http://github.com/api
                  expectedStatusCode: 202
                  expectedResponseBodyPattern: .*
                  ignoreCertificateErrors: true
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var httpMonitor = Assert.IsType<HttpMonitor>(monitor);
        Assert.Equal("mhttp", httpMonitor.Options.Name);
        Assert.Equal(new Uri("http://github.com/api"), httpMonitor.Options.Url);
        Assert.Equal(HttpStatusCode.Accepted, httpMonitor.Options.ExpectedStatusCode);
        Assert.Equal(".*", httpMonitor.Options.ExpectedResponseBodyPattern);
        Assert.Equal(true, httpMonitor.Options.IgnoreCertificateErrors);
    }

    [Fact]
    public void MonitorPing_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mping:
                !ping
                options:
                  host: 192.168.1.1
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var pingMonitor = Assert.IsType<PingMonitor>(monitor);
        Assert.Equal("mping", pingMonitor.Options.Name);
        Assert.Equal("192.168.1.1", pingMonitor.Options.Host);
    }

    [Fact]
    public void MonitorTcp_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mtcp:
                !tcp
                options:
                  host: 192.168.1.2
                  port: 65000
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var tcpMonitor = Assert.IsType<TcpMonitor>(monitor);
        Assert.Equal("mtcp", tcpMonitor.Options.Name);
        Assert.Equal("192.168.1.2", tcpMonitor.Options.Host);
        Assert.Equal(65000, tcpMonitor.Options.Port);
    }

    private static ConfigurationErrorException CatchConfigurationException(string configuration) =>
        Assert.Throws<ConfigurationErrorException>(() =>
        {
            using var reader = new StringReader(configuration);
            ApplicationConfiguration.ReadFromStream(reader);
        });

    private static ApplicationConfiguration ReadConfiguration(string configuration)
    {
        using var reader = new StringReader(configuration);
        return ApplicationConfiguration.ReadFromStream(reader);
    }

    private static IMonitor<MonitorOptions> ReadSingleMonitorFromConfiguration(string ConfigurationContent)
    {
        var configuration = ReadConfiguration(ConfigurationContent);
        Assert.Single(configuration.Monitors);

        var monitor = configuration.Monitors.First().Value;
        Assert.NotNull(monitor);

        return monitor;
    }
}
