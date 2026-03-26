using System.ComponentModel.DataAnnotations;
using Deucalion.Configuration;
using YamlDotNet.Serialization;

namespace Deucalion.Network.Configuration;

[YamlSerializable]
public record PingMonitorConfiguration : PullMonitorConfiguration
{
    [Required]
    public required string Host { get; set; }
}
