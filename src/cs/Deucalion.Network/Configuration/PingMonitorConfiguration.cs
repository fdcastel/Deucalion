using System.ComponentModel.DataAnnotations;
using Deucalion.Configuration;

namespace Deucalion.Network.Configuration;

public record PingMonitorConfiguration : PullMonitorConfiguration
{
    [Required]
    public required string Host { get; set; }
}
