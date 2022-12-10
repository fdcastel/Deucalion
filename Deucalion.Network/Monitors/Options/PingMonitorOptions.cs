using System.ComponentModel.DataAnnotations;
using Deucalion.Monitors.Options;

namespace Deucalion.Network.Monitors.Options;

public class PingMonitorOptions : PullMonitorOptions
{
    [Required]
    public string Host { get; set; } = default!;
}
