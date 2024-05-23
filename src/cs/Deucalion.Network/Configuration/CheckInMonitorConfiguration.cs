using Deucalion.Configuration;

namespace Deucalion.Network.Configuration;

public record CheckInMonitorConfiguration : PushMonitorConfiguration
{
    public string? Secret { get; set; }
}
