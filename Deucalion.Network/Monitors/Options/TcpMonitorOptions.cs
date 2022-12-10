using System.ComponentModel.DataAnnotations;
using Deucalion.Monitors.Options;

namespace Deucalion.Network.Monitors.Options;

public class TcpMonitorOptions : PullMonitorOptions
{
    [Required]
    public string Host { get; set; } = default!;
    [Required]
    public int Port { get; set; } = default!;
}
