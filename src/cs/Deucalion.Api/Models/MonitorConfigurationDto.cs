using Deucalion.Configuration;
using Deucalion.Network.Configuration;

namespace Deucalion.Api.Models;

internal record MonitorConfigurationDto(
    string Type,
    string? Group,
    string? Href,
    string? Image,
    string[]? Tags
)
{
    internal static MonitorConfigurationDto From(PullMonitorConfiguration c) =>
        new(
            Type: ExtractType(c),
            Group: c.Group,
            Href: ExtractHref(c),
            Image: c.Image,
            Tags: c.Tags
        );

    private static string ExtractType(PullMonitorConfiguration c) => c switch
    {
        HttpMonitorConfiguration => "http",
        DnsMonitorConfiguration => "dns",
        PingMonitorConfiguration => "ping",
        TcpMonitorConfiguration => "tcp",
        CheckInMonitorConfiguration => "checkin",
        _ => "unknown"
    };

    private static string? ExtractHref(PullMonitorConfiguration monitor)
    {
        if (monitor.Href is null && monitor is HttpMonitorConfiguration hm)
        {
            return hm.Url.ToString();
        }

        if (string.IsNullOrWhiteSpace(monitor.Href))
        {
            return null;
        }

        return monitor.Href;
    }
}
