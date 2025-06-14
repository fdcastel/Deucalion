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
                select KeyValuePair.Create(kvp.Key, MonitorFromConfiguration(kvp.Value))
            )
        };

    private static Monitors.PullMonitor MonitorFromConfiguration(PullMonitorConfiguration monitorConfiguration) =>
        monitorConfiguration switch
        {
            CheckInMonitorConfiguration checkInMonitorConfiguration => checkInMonitorConfiguration.Build(),

            DnsMonitorConfiguration dnsMonitorConfiguration => dnsMonitorConfiguration.Build(),
            HttpMonitorConfiguration httpMonitorConfiguration => httpMonitorConfiguration.Build(),
            PingMonitorConfiguration pingMonitorConfiguration => pingMonitorConfiguration.Build(),
            TcpMonitorConfiguration tcpMonitorConfiguration => tcpMonitorConfiguration.Build(),

            _ => throw new NotImplementedException("Unknown MonitorConfiguration."),
        };
}
