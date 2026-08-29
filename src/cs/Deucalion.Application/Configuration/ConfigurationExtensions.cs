using Deucalion.Configuration;
using Deucalion.Monitors;
using Deucalion.Network.Configuration;
using Deucalion.Network.Monitors;

namespace Deucalion.Application.Configuration;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Builds the live monitors from a parsed configuration, keyed by monitor name.
    /// </summary>
    public static IReadOnlyDictionary<string, PullMonitor> BuildMonitors(this ApplicationConfiguration configuration) =>
        new Dictionary<string, PullMonitor>(
            from kvp in configuration.Monitors
            select KeyValuePair.Create(kvp.Key, BuildMonitor(kvp.Key, kvp.Value))
        );

    private static PullMonitor BuildMonitor(string monitorName, PullMonitorConfiguration configuration)
    {
        try
        {
            return MonitorFromConfiguration(configuration);
        }
        catch (ConfigurationErrorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Anything that only fails while constructing the live monitor -- a malformed
            // 'url:' (UriFormatException), an invalid 'expectedResponseBodyPattern'
            // (RegexParseException) -- is a configuration error, not a crash.
            throw new ConfigurationErrorException($"Monitor '{monitorName}': {ex.Message}", ex);
        }
    }

    private static PullMonitor MonitorFromConfiguration(PullMonitorConfiguration monitorConfiguration) =>
        monitorConfiguration switch
        {
            CheckInMonitorConfiguration checkInMonitorConfiguration => checkInMonitorConfiguration.Build(),

            DnsMonitorConfiguration dnsMonitorConfiguration => dnsMonitorConfiguration.Build(),
            HttpMonitorConfiguration httpMonitorConfiguration => httpMonitorConfiguration.Build(),
            PingMonitorConfiguration pingMonitorConfiguration => pingMonitorConfiguration.Build(),
            TcpMonitorConfiguration tcpMonitorConfiguration => tcpMonitorConfiguration.Build(),

            _ => throw new ConfigurationErrorException(
                string.Format(ApplicationConfiguration.Messages.ConfigurationUnknownMonitorType, monitorConfiguration.Name)),
        };

    internal static CheckInMonitor Build(this CheckInMonitorConfiguration configuration)
    {
        var monitor = new CheckInMonitor();
        monitor.Secret = configuration.Secret;
        monitor.IntervalToDown = configuration.IntervalToDown ?? monitor.IntervalToDown;
        monitor.IntervalWhenUp = monitor.IntervalToDown;
        monitor.IntervalWhenDown = monitor.IntervalToDown;
        monitor.Name = configuration.Name ?? monitor.Name;
        monitor.IgnoreFailCount = configuration.IgnoreFailCount ?? monitor.IgnoreFailCount;
        monitor.UpsideDown = configuration.UpsideDown ?? monitor.UpsideDown;
        return monitor;
    }

    internal static DnsMonitor Build(this DnsMonitorConfiguration configuration)
    {
        var monitor = new DnsMonitor() { Host = configuration.Host };
        monitor.RecordType = configuration.RecordType ?? monitor.RecordType;
        monitor.Resolver = configuration.Resolver ?? monitor.Resolver;
        ConfigurePullMonitor(monitor, configuration);
        return monitor;
    }

    internal static HttpMonitor Build(this HttpMonitorConfiguration configuration)
    {
        var monitor = new HttpMonitor() { Url = new Uri(configuration.Url) };
        monitor.ExpectedStatusCode = configuration.ExpectedStatusCode ?? monitor.ExpectedStatusCode;
        monitor.ExpectedResponseBodyPattern = configuration.ExpectedResponseBodyPattern ?? monitor.ExpectedResponseBodyPattern;
        monitor.IgnoreCertificateErrors = configuration.IgnoreCertificateErrors ?? monitor.IgnoreCertificateErrors;
        monitor.Method = configuration.Method ?? monitor.Method;
        ConfigurePullMonitor(monitor, configuration);
        return monitor;
    }

    internal static PingMonitor Build(this PingMonitorConfiguration configuration)
    {
        var monitor = new PingMonitor() { Host = configuration.Host };
        ConfigurePullMonitor(monitor, configuration);
        return monitor;
    }

    internal static TcpMonitor Build(this TcpMonitorConfiguration configuration)
    {
        var monitor = new TcpMonitor() { Host = configuration.Host, Port = configuration.Port };
        ConfigurePullMonitor(monitor, configuration);
        return monitor;
    }

    private static void ConfigurePullMonitor(PullMonitor monitor, PullMonitorConfiguration configuration)
    {
        monitor.IntervalWhenUp = configuration.IntervalWhenUp ?? monitor.IntervalWhenUp;
        monitor.IntervalWhenDown = configuration.IntervalWhenDown ?? monitor.IntervalWhenDown;
        monitor.Timeout = configuration.Timeout ?? monitor.Timeout;
        monitor.WarnTimeout = configuration.WarnTimeout;
        monitor.Name = configuration.Name ?? monitor.Name;
        monitor.IgnoreFailCount = configuration.IgnoreFailCount ?? monitor.IgnoreFailCount;
        monitor.UpsideDown = configuration.UpsideDown ?? monitor.UpsideDown;
    }
}
