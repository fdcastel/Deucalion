using System.ComponentModel.DataAnnotations;
using System.Net;
using Deucalion.Monitors.Configuration;
using DnsClient;

namespace Deucalion.Network.Monitors;

public record DnsMonitorOptionalConfiguration : PullMonitorConfiguration
{
    public QueryType? RecordType { get; set; }

    public IPEndPoint? Resolver { get; set; }
}

public record DnsMonitorConfiguration : DnsMonitorOptionalConfiguration
{
    [Required]
    public required string Host { get; set; }
}
