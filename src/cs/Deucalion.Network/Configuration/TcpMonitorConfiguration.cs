using System.ComponentModel.DataAnnotations;
using Deucalion.Monitors.Configuration;

namespace Deucalion.Network.Monitors;

public record TcpMonitorConfiguration : PullMonitorConfiguration
{
    [Required]
    public required string Host { get; set; }

    [Required]
    public required int Port { get; set; }
}
