using System.ComponentModel.DataAnnotations;
using System.Net;
using Deucalion.Configuration;
using DnsClient;
using YamlDotNet.Serialization;

namespace Deucalion.Network.Configuration;

[YamlSerializable]
public record DnsMonitorOptionalConfiguration : PullMonitorConfiguration
{
    public QueryType? RecordType { get; set; }

    public IPEndPoint? Resolver { get; set; }
}

[YamlSerializable]
public record DnsMonitorConfiguration : DnsMonitorOptionalConfiguration
{
    [Required]
    public required string Host { get; set; }
}
