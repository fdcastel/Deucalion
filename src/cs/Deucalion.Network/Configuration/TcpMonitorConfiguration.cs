using System.ComponentModel.DataAnnotations;
using Deucalion.Configuration;

namespace Deucalion.Network.Configuration;

public record TcpMonitorConfiguration : PullMonitorConfiguration
{
    [Required]
    public required string Host { get; set; }

    // [Required] is a no-op on a non-nullable int: 'port: 0' or 'port: 99999' passed validation
    // and then threw ArgumentOutOfRangeException on every probe (#23). Range does the real check.
    [Range(1, 65535)]
    public required int Port { get; set; }
}
