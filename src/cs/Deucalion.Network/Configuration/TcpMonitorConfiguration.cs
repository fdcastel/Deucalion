using System.ComponentModel.DataAnnotations;
using Deucalion.Configuration;
using YamlDotNet.Serialization;

namespace Deucalion.Network.Configuration;

[YamlSerializable]
public record TcpMonitorConfiguration : PullMonitorConfiguration
{
    [Required]
    public required string Host { get; set; }

    [Required]
    public required int Port { get; set; }
}
