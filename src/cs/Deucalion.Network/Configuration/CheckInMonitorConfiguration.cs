using Deucalion.Configuration;
using YamlDotNet.Serialization;

namespace Deucalion.Network.Configuration;

[YamlSerializable]
public record CheckInMonitorConfiguration : PullMonitorConfiguration
{
    public string? Secret { get; set; }
    public TimeSpan? IntervalToDown { get; set; }
}
