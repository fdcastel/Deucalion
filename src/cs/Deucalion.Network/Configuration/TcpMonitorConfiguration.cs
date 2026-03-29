using System.ComponentModel.DataAnnotations;
using Deucalion.Configuration;

namespace Deucalion.Network.Configuration;

public record TcpMonitorConfiguration : PullMonitorConfiguration
{
    [Required]
    public required string Host { get; set; }

    [Required]
    public required int Port { get; set; }
}
