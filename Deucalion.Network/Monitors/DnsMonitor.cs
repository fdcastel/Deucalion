using System.ComponentModel.DataAnnotations;
using System.Net;
using Deucalion.Monitors;
using DnsClient;

namespace Deucalion.Network.Monitors;

public class DnsMonitor : PullMonitor
{
    private static readonly TimeSpan DefaultDnsTimeout = TimeSpan.FromMilliseconds(500);

    public static readonly int DefaultDnsPort = 53;
    public static readonly QueryType DefaultRecordType = QueryType.A;

    [Required]
    public string Host { get; set; } = default!;

    public QueryType? RecordType { get; set; }

    private IPEndPoint? _resolver;
    public IPEndPoint? Resolver
    {
        get => _resolver;
        set => _resolver = value is not null
            ? value.Port == 0
                ? new IPEndPoint(value.Address, DefaultDnsPort)
                : value
            : null;
    }

    public QueryType RecordTypeOrDefault => RecordType ?? DefaultRecordType;

    public override async Task<MonitorResponse> QueryAsync()
    {
        try
        {
            LookupClientOptions options = Resolver is not null
                ? new(Resolver)
                : new();

            options.Timeout = Timeout ?? DefaultDnsTimeout;

            LookupClient lookup = new(options);
            var result = await lookup.QueryAsync(Host, RecordType ?? QueryType.A);

            return result.Answers.Any()
                ? MonitorResponse.DefaultUp
                : MonitorResponse.DefaultDown;
        }
        catch (DnsResponseException)
        {
            return MonitorResponse.DefaultDown;
        }
    }
}
