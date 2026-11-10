namespace Deucalion.Network.Monitors;

/// <summary>
/// The DNS record types a <see cref="DnsMonitor"/> can query: the ones
/// <see cref="System.Net.DnsResolver"/> resolves. Member names match the YAML
/// <c>recordType</c> values.
/// </summary>
public enum DnsRecordType
{
    A,
    AAAA,
    CNAME,
    MX,
    NS,
    PTR,
    SRV,
    TXT,
}
