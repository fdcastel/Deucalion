using System.ComponentModel.DataAnnotations;
using System.Net;
using Deucalion.Configuration;
using Deucalion.Network.Monitors;

namespace Deucalion.Network.Configuration;

public record DnsMonitorOptionalConfiguration : PullMonitorConfiguration
{
    public DnsRecordType? RecordType { get; set; }

    public IPEndPoint? Resolver { get; set; }
}

public record DnsMonitorConfiguration : DnsMonitorOptionalConfiguration
{
    [Required]
    public required string Host { get; set; }
}
