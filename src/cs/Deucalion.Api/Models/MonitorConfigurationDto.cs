using Deucalion.Configuration;
using Deucalion.Network.Configuration;

namespace Deucalion.Api.Models;

internal record MonitorConfigurationDto(
    string? Group,
    string? Href,
    string? Image,
    string[]? Tags
)
{
    internal static MonitorConfigurationDto From(PullMonitorConfiguration c) =>
        new(
            Group: c.Group,
            Href: ExtractHref(c),
            Image: c.Image,
            Tags: c.Tags
        );

    private static string? ExtractHref(PullMonitorConfiguration monitor)
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
