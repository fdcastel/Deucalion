using System.ComponentModel.DataAnnotations;
using Deucalion.Monitors.Configuration;

namespace Deucalion.Network.Monitors;

public record PingMonitorConfiguration : PullMonitorConfiguration
{
    [Required]
    public required string Host { get; set; }
}
