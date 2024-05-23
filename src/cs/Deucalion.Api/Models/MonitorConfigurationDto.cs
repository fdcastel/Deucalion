using Deucalion.Configuration;
using Deucalion.Network.Configuration;

namespace Deucalion.Api.Models;

internal record MonitorConfigurationDto(
    string? Group,
    string? Href,
    string? Image
)
{
    internal static MonitorConfigurationDto From(MonitorConfiguration c) =>
        new(
            Group: c.Group,
            Href: ExtractHref(c),
            Image: c.Image
        );

    private static string? ExtractHref(MonitorConfiguration monitor)
    {
        if (monitor.Href is null && monitor is HttpMonitorConfiguration hm)
        {
            return hm.Url.ToString();
        }

        if (string.IsNullOrWhiteSpace(monitor.Href))
        {
            return string.Empty;
        }

        return monitor.Href;
    }
}
