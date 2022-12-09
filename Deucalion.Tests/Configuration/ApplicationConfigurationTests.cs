using System.Net;
using Deucalion.Application.Configuration;
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
    public void Monitor_WithoutClass_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              m1:
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationMonitorMustHaveClass, "m1"), exception.Message);
    }

    [Fact]
    public void Monitor_WithClassNotFound_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              m2:
                class: notfound
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationMonitorClassNotFound, "m2", "notfound"), exception.Message);
    }

    [Fact]
    public void MonitorDns_WithoutHost_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              mdns:
                class: dns
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationMonitorMustHaveHost, "mdns"), exception.Message);
    }

    [Fact]
    public void MonitorHttp_WithoutUrl_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              mhttp:
                class: http
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationMonitorMustHaveUrl, "mhttp"), exception.Message);
    }

    [Fact]
    public void MonitorPing_WithoutHost_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              mping:
                class: ping
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationMonitorMustHaveHost, "mping"), exception.Message);
    }

    [Fact]
    public void MonitorTcp_WithoutHost_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              mtcp:
                class: tcp
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationMonitorMustHaveHost, "mtcp"), exception.Message);
    }

    [Fact]
    public void MonitorTcp_WithoutPort_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              mtcp:
                class: tcp
                host: google.com
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationMonitorMustHavePort, "mtcp"), exception.Message);
    }

    [Fact]
    public void MonitorDns_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mdns:
                class: dns
                host: google.com
                recordType: A
                resolver: 8.8.8.8
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        Assert.Equal("dns", monitor.ClassName);
        Assert.Equal("mdns", monitor.MonitorName);
        Assert.Equal("google.com", monitor.Host);
        Assert.Equal(QueryType.A, monitor.RecordType);
        Assert.Equal(IPEndPoint.Parse("8.8.8.8:0"), monitor.Resolver);
    }

    [Fact]
    public void MonitorHttp_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mhttp:
                class: http
                url: http://github.com/api
                expectedStatusCode: 202
                expectedResponseBodyPattern: .*/.*
                ignoreCertificateErrors: true
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        Assert.Equal("http", monitor.ClassName);
        Assert.Equal("mhttp", monitor.MonitorName);
        Assert.Equal(new Uri("http://github.com/api"), monitor.Url);
        Assert.Equal(HttpStatusCode.Accepted, monitor.ExpectedStatusCode);
        Assert.Equal(".*/.*", monitor.ExpectedResponseBodyPattern);
        Assert.Equal(true, monitor.IgnoreCertificateErrors);
    }

    [Fact]
    public void MonitorPing_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mping:
                class: ping
                host: 192.168.1.1
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        Assert.Equal("ping", monitor.ClassName);
        Assert.Equal("mping", monitor.MonitorName);
        Assert.Equal("192.168.1.1", monitor.Host);
    }

    [Fact]
    public void MonitorTcp_CanDeserialize_FromConfiguration()
    {
        const string ConfigurationContent = @"
            monitors:
              mtcp:
                class: tcp
                host: 192.168.1.2
                port: 65000
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        Assert.Equal("tcp", monitor.ClassName);
        Assert.Equal("mtcp", monitor.MonitorName);
        Assert.Equal("192.168.1.2", monitor.Host);
        Assert.Equal(65000, monitor.Port);
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

    private static MonitorConfiguration ReadSingleMonitorFromConfiguration(string ConfigurationContent)
    {
        var configuration = ReadConfiguration(ConfigurationContent);
        Assert.Single(configuration.Monitors);

        var monitor = configuration.Monitors.First().Value;
        Assert.NotNull(monitor);

        return monitor;
    }
}
