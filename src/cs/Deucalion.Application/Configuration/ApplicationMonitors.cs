using Deucalion.Monitors;
using Deucalion.Monitors.Configuration;
using Deucalion.Network.Monitors;

namespace Deucalion.Application.Configuration;

public record ApplicationMonitors
{
    public required Dictionary<string, MonitorBase> Monitors { get; set; }

    public static ApplicationMonitors BuildFrom(ApplicationConfiguration configuration) => new()
    {
        Monitors = new Dictionary<string, MonitorBase>(
            from kvp in configuration.Monitors
            select KeyValuePair.Create(kvp.Key, MonitorFromConfiguration(kvp.Value))
        )
    };

    private static MonitorBase MonitorFromConfiguration(MonitorConfiguration mc) => mc switch
    {
        CheckInMonitorConfiguration cmc => cmc.Build(),

        DnsMonitorConfiguration dmc => dmc.Build(),
        HttpMonitorConfiguration hmc => hmc.Build(),
        PingMonitorConfiguration pmc => pmc.Build(),
        TcpMonitorConfiguration tmc => tmc.Build(),

        _ => throw new NotImplementedException("Unknown MonitorConfiguration."),
    };
}
