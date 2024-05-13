using Deucalion.Monitors;
using Deucalion.Network.Monitors;

namespace Deucalion.Api.Models;

internal record MonitorConfigurationDto(
    string? Group,
    string? Href,
    string? Image
)
{
    internal static MonitorConfigurationDto From(MonitorBase c) =>
        new(
            Group: c.Group,
            Href: ExtractHref(c),
            Image: c.Image
        );

    private static string? ExtractHref(MonitorBase monitor)
    {
        if (monitor.Href is null && monitor is HttpMonitor hm)
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
