using System.ComponentModel.DataAnnotations;
using Deucalion.Configuration;
using YamlDotNet.Serialization;

namespace Deucalion.Network.Configuration;

[YamlSerializable]
public record TcpMonitorConfiguration : PullMonitorConfiguration
{
    [Required]
    public string Host { get; set; } = null!;

    [Required]
    public int Port { get; set; }
}
