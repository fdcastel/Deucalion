using Deucalion.Configuration;
using Deucalion.Network.Configuration;

namespace Deucalion.Application.Configuration;

public record ApplicationMonitors
{
    public required Dictionary<string, Monitors.PullMonitor> Monitors { get; set; }

    public static ApplicationMonitors BuildFrom(ApplicationConfiguration configuration) =>
        new()
        {
            Monitors = new Dictionary<string, Monitors.PullMonitor>(
                from kvp in configuration.Monitors
                select KeyValuePair.Create(kvp.Key, BuildMonitor(kvp.Key, kvp.Value))
            )
        };

    private static Monitors.PullMonitor BuildMonitor(string monitorName, PullMonitorConfiguration configuration)
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

    private static Monitors.PullMonitor MonitorFromConfiguration(PullMonitorConfiguration monitorConfiguration) =>
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
}
