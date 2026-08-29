using System.Net;
using Deucalion.Application.Configuration;
using Deucalion.Configuration;
using Deucalion.Network.Configuration;
using DnsClient;
using Xunit;

namespace Deucalion.Tests.Configuration;

public class ConfigurationTests
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
    public void DnsMonitor_CanDeserialize()
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
        var dnsMonitor = Assert.IsType<DnsMonitorConfiguration>(monitor);
        Assert.Equal("mdns", dnsMonitor.Name);
        Assert.Equal("google.com", dnsMonitor.Host);
        Assert.Equal(QueryType.A, dnsMonitor.RecordType);
        Assert.Equal(IPEndPoint.Parse("1.1.1.1"), dnsMonitor.Resolver);
    }

    [Fact]
    public void DnsMonitor_CanDeserializeResolverPort()
    {
        const string ConfigurationContent = @"
            monitors:
              mdns:
                !dns
                host: google.com
                recordType: A
                resolver: 1.1.1.1:66
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var dnsMonitor = Assert.IsType<DnsMonitorConfiguration>(monitor);
        Assert.Equal(IPEndPoint.Parse("1.1.1.1:66"), dnsMonitor.Resolver);
    }

    [Fact]
    public void DnsMonitor_CanUseDefaultValues()
    {
        const string ConfigurationContent = @"
            defaults:
              dns:
                recordType: AAAA
                resolver: 8.8.8.8

            monitors:
              mdns:
                !dns
                host: google.com
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var dnsMonitor = Assert.IsType<DnsMonitorConfiguration>(monitor);
        Assert.Equal(QueryType.AAAA, dnsMonitor.RecordType);
        Assert.Equal(IPEndPoint.Parse("8.8.8.8"), dnsMonitor.Resolver);
    }

    [Fact]
    public void HttpMonitor_CanDeserialize()
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
        var httpMonitor = Assert.IsType<HttpMonitorConfiguration>(monitor);
        Assert.Equal("mhttp", httpMonitor.Name);
        Assert.Equal("http://github.com/api", httpMonitor.Url);
        Assert.Equal(HttpStatusCode.Accepted, httpMonitor.ExpectedStatusCode);
        Assert.Equal(".*", httpMonitor.ExpectedResponseBodyPattern);
        Assert.Equal(true, httpMonitor.IgnoreCertificateErrors);
    }

    [Fact]
    public void HttpMonitor_CanUseDefaultValues()
    {
        const string ConfigurationContent = @"
            defaults:
              http:
                expectedStatusCode: 202
                ignoreCertificateErrors: true

            monitors:
              mhttp:
                !http
                url: https://google.com
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var httpMonitor = Assert.IsType<HttpMonitorConfiguration>(monitor);
        Assert.Equal(true, httpMonitor.IgnoreCertificateErrors);
        Assert.Equal(HttpStatusCode.Accepted, httpMonitor.ExpectedStatusCode);
    }

    [Fact]
    public void PingMonitor_CanDeserialize()
    {
        const string ConfigurationContent = @"
            monitors:
              mping:
                !ping
                host: 192.168.1.1
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var pingMonitor = Assert.IsType<PingMonitorConfiguration>(monitor);
        Assert.Equal("mping", pingMonitor.Name);
        Assert.Equal("192.168.1.1", pingMonitor.Host);
    }

    [Fact]
    public void TcpMonitor_CanDeserialize()
    {
        const string ConfigurationContent = @"
            monitors:
              mtcp:
                !tcp
                host: 192.168.1.2
                port: 65000
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var tcpMonitor = Assert.IsType<TcpMonitorConfiguration>(monitor);
        Assert.Equal("mtcp", tcpMonitor.Name);
        Assert.Equal("192.168.1.2", tcpMonitor.Host);
        Assert.Equal(65000, tcpMonitor.Port);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(70000)]
    public void Issue23_TcpMonitor_PortOutOfRange_Throws(int port)
    {
        // [Required] never fires on a non-nullable int, so these used to load fine and fail on
        // every probe with ArgumentOutOfRangeException.
        var configurationContent = $@"
            monitors:
              mtcp:
                !tcp
                host: 192.168.1.2
                port: {port}
        ";

        var exception = CatchConfigurationException(configurationContent);
        Assert.Contains("mtcp", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Port", exception.Message, StringComparison.Ordinal);
        Assert.Contains("65535", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void TcpMonitor_PortAtRangeBounds_IsAccepted(int port)
    {
        var configurationContent = $@"
            monitors:
              mtcp:
                !tcp
                host: 192.168.1.2
                port: {port}
        ";

        var tcpMonitor = Assert.IsType<TcpMonitorConfiguration>(ReadSingleMonitorFromConfiguration(configurationContent));
        Assert.Equal(port, tcpMonitor.Port);
    }

    [Theory]
    [InlineData("events")]
    [InlineData("Events")]
    [InlineData("EVENTS")]
    public void Issue23_ReservedMonitorName_Throws(string monitorName)
    {
        // '/api/monitors/events' is the SSE stream; a monitor with that name could never be
        // fetched individually. Routing is case-insensitive, so the check is too.
        var configurationContent = $@"
            monitors:
              {monitorName}:
                !ping
                host: 192.168.1.1
        ";

        var exception = CatchConfigurationException(configurationContent);
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationReservedMonitorName, monitorName, "events"), exception.Message);
    }

    [Fact]
    public void MonitorNameContainingReservedWord_IsAccepted()
    {
        const string ConfigurationContent = @"
            monitors:
              events-api:
                !ping
                host: 192.168.1.1
        ";

        Assert.Equal("events-api", ReadSingleMonitorFromConfiguration(ConfigurationContent).Name);
    }

    [Fact]
    public void CheckInMonitor_CanDeserialize()
    {
        const string ConfigurationContent = @"
            monitors:
              mcheckin:
                !checkin
                secret: passw0rd
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var checkInMonitor = Assert.IsType<CheckInMonitorConfiguration>(monitor);
        Assert.Equal("mcheckin", checkInMonitor.Name);
        Assert.Equal("passw0rd", checkInMonitor.Secret);
    }

    [Fact]
    public void Monitors_CanUseDefaultValues()
    {
        const string ConfigurationContent = @"
            defaults:
              intervalWhenDown: 00:00:10
              intervalWhenUp: 00:00:20
              ping:
                warnTimeout: 00:00:30
                timeout: 00:00:40

            monitors:
              mping:
                !ping
                host: 192.168.1.1

              mhttp:
                !http
                url: https://google.com
        ";

        var monitors = ReadConfiguration(ConfigurationContent);

        var pingMonitor = Assert.IsType<PingMonitorConfiguration>(monitors.Monitors.ElementAt(0).Value);
        Assert.Equal("192.168.1.1", pingMonitor.Host);
        Assert.Equal(TimeSpan.FromSeconds(10), pingMonitor.IntervalWhenDown);
        Assert.Equal(TimeSpan.FromSeconds(20), pingMonitor.IntervalWhenUp);
        Assert.Equal(TimeSpan.FromSeconds(30), pingMonitor.WarnTimeout);
        Assert.Equal(TimeSpan.FromSeconds(40), pingMonitor.Timeout);

        var httpMonitor = Assert.IsType<HttpMonitorConfiguration>(monitors.Monitors.ElementAt(1).Value);
        Assert.Equal("https://google.com", httpMonitor.Url);
        Assert.Equal(TimeSpan.FromSeconds(10), httpMonitor.IntervalWhenDown);
        Assert.Equal(TimeSpan.FromSeconds(20), httpMonitor.IntervalWhenUp);
        Assert.Null(httpMonitor.WarnTimeout);
        Assert.Null(httpMonitor.Timeout);
    }

    [Fact]
    public void Monitors_CanUseCustomValues()
    {
        const string ConfigurationContent = @"
            monitors:
              mping:
                !ping
                host: 192.168.1.1
                intervalWhenDown: 00:00:10
                intervalWhenUp: 00:00:20
                warnTimeout: 00:00:30
                timeout: 00:00:40
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var pingMonitor = Assert.IsType<PingMonitorConfiguration>(monitor);
        Assert.Equal(TimeSpan.FromSeconds(10), pingMonitor.IntervalWhenDown);
        Assert.Equal(TimeSpan.FromSeconds(20), pingMonitor.IntervalWhenUp);
        Assert.Equal(TimeSpan.FromSeconds(30), pingMonitor.WarnTimeout);
        Assert.Equal(TimeSpan.FromSeconds(40), pingMonitor.Timeout);
    }

    [Fact]
    public void Monitors_CanUseInterpolation()
    {
        const string ConfigurationContent = """
            monitors:
              cloudflare: !dns
                recordType: A
                host: ${MONITOR_NAME}.com

              bing: !ping
                timeout: 00:00:40
                host: "${MONITOR_NAME}.com"

              google: !http
                url: https://${MONITOR_NAME}.com
        """;

        var monitors = ReadConfiguration(ConfigurationContent);

        var dnsMonitor = Assert.IsType<DnsMonitorConfiguration>(monitors.Monitors.ElementAt(0).Value);
        Assert.Equal("cloudflare.com", dnsMonitor.Host);

        var pingMonitor = Assert.IsType<PingMonitorConfiguration>(monitors.Monitors.ElementAt(1).Value);
        Assert.Equal("bing.com", pingMonitor.Host);

        var httpMonitor = Assert.IsType<HttpMonitorConfiguration>(monitors.Monitors.ElementAt(2).Value);
        Assert.Equal("https://google.com", httpMonitor.Url);
    }

    [Fact]
    public void ReadFromFile_MissingFile_ThrowsWithFriendlyMessage()
    {
        var missingFile = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.yaml");

        var exception = Assert.Throws<ConfigurationErrorException>(() => ApplicationConfiguration.ReadFromFile(missingFile));
        Assert.Equal(string.Format(ApplicationConfiguration.Messages.ConfigurationFileNotFound, missingFile), exception.Message);
    }

    [Fact]
    public void ReadFromFile_MalformedYaml_ThrowsWithFriendlyMessage()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid()}.yaml");
        try
        {
            File.WriteAllText(tempFile, "monitors:\n  m1: !http\n    url: [invalid");

            var exception = Assert.Throws<ConfigurationErrorException>(() => ApplicationConfiguration.ReadFromFile(tempFile));
            Assert.StartsWith(string.Format(ApplicationConfiguration.Messages.ConfigurationFileParseError, tempFile, "").TrimEnd(), exception.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("intervalWhenUp: 00:00:00")]
    [InlineData("intervalWhenDown: -00:00:01")]
    [InlineData("timeout: 00:00:00")]
    [InlineData("warnTimeout: -00:05:00")]
    public void Monitor_ZeroOrNegativeTimeSpan_Throws(string fieldLine)
    {
        var configurationContent = $@"
            monitors:
              mping:
                !ping
                host: 192.168.1.1
                {fieldLine}
        ";

        Assert.Throws<ConfigurationErrorException>(() => ApplicationConfiguration.ReadFromString(configurationContent));
    }

    [Fact]
    public void CheckInMonitor_ZeroIntervalToDown_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              mcheckin:
                !checkin
                secret: passw0rd
                intervalToDown: 00:00:00
        ";

        Assert.Throws<ConfigurationErrorException>(() => ApplicationConfiguration.ReadFromString(ConfigurationContent));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("HEAD")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public void HttpMonitor_MethodIsDeserializedByHttpMethodConverter(string method)
    {
        var configurationContent = $@"
            monitors:
              mhttp:
                !http
                url: http://github.com/api
                method: {method}
        ";

        var monitor = ReadSingleMonitorFromConfiguration(configurationContent);
        var httpMonitor = Assert.IsType<HttpMonitorConfiguration>(monitor);
        Assert.Equal(method, httpMonitor.Method?.Method);
    }

    [Fact]
    public void HttpMonitor_MethodOmitted_StaysNullSoTheMonitorDefaultsToGet()
    {
        const string ConfigurationContent = @"
            monitors:
              mhttp:
                !http
                url: http://github.com/api
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var httpMonitor = Assert.IsType<HttpMonitorConfiguration>(monitor);
        Assert.Null(httpMonitor.Method);
    }

    [Fact]
    public void HttpMonitor_MethodCanComeFromTheDefaultsBlock()
    {
        const string ConfigurationContent = @"
            defaults:
              http:
                method: HEAD

            monitors:
              mhttp:
                !http
                url: http://github.com/api
        ";

        var monitor = ReadSingleMonitorFromConfiguration(ConfigurationContent);
        var httpMonitor = Assert.IsType<HttpMonitorConfiguration>(monitor);
        Assert.Equal("HEAD", httpMonitor.Method?.Method);
    }

    private static ConfigurationErrorException CatchConfigurationException(string configurationContent) =>
        Assert.Throws<ConfigurationErrorException>(() => ApplicationConfiguration.ReadFromString(configurationContent));

    [Theory]
    [InlineData("mhttp", "!http", "expectedStatusCode: 200")]      // missing url
    [InlineData("mping", "!ping", "timeout: 00:00:05")]            // missing host
    [InlineData("mtcp", "!tcp", "port: 8080")]                     // missing host
    [InlineData("mdns", "!dns", "recordType: A")]                  // missing host
    public void Monitor_MissingRequiredField_Throws(string monitorName, string tag, string fieldLine)
    {
        var configurationContent = $@"
            monitors:
              {monitorName}:
                {tag}
                {fieldLine}
        ";

        CatchConfigurationException(configurationContent);
    }

    [Fact]
    public void InvalidTypeTag_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              mfoo:
                !notavalidtype
                host: example.com
        ";

        // SharpYaml falls back to the base type for unknown tags; we must reject that
        // up front rather than let it fail later while building the live monitor.
        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Contains("mfoo", exception.Message, StringComparison.Ordinal);
        Assert.Contains("!ping", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MonitorWithoutTypeTag_Throws()
    {
        const string ConfigurationContent = @"
            monitors:
              mbase:
                intervalWhenUp: 00:00:10
        ";

        var exception = CatchConfigurationException(ConfigurationContent);
        Assert.Contains("mbase", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpMonitor_MalformedUrl_ThrowsWhenBuildingMonitors()
    {
        const string ConfigurationContent = @"
            monitors:
              mhttp:
                !http
                url: 'not a url'
        ";

        var configuration = ApplicationConfiguration.ReadFromString(ConfigurationContent);

        var exception = Assert.Throws<ConfigurationErrorException>(() => configuration.BuildMonitors());
        Assert.Contains("mhttp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoMonitorsSection_Throws()
    {
        const string ConfigurationContent = @"
            defaults:
              intervalWhenUp: 00:00:10
        ";

        CatchConfigurationException(ConfigurationContent);
    }

    private static ApplicationConfiguration ReadConfiguration(string configurationContent)
    {
        return ApplicationConfiguration.ReadFromString(configurationContent);
    }

    private static PullMonitorConfiguration ReadSingleMonitorFromConfiguration(string configurationContent)
    {
        var configuration = ReadConfiguration(configurationContent);
        Assert.Single(configuration.Monitors);

        var monitor = configuration.Monitors.First().Value;
        Assert.NotNull(monitor);

        return monitor;
    }
}
