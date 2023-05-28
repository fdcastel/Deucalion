using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
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
        LookupClientOptions options = Resolver is not null
            ? new(Resolver)
            : new();

        options.Timeout = Timeout ?? DefaultDnsTimeout;

        LookupClient lookup = new(options);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await lookup.QueryAsync(Host, RecordType ?? QueryType.A);
            stopwatch.Stop();

            return result.HasError
                ? MonitorResponse.Down(stopwatch.Elapsed, result.ErrorMessage)
                : MonitorResponse.Up(stopwatch.Elapsed, result.Answers[0].ToString());
        }
        catch (DnsResponseException e)
        {
            return MonitorResponse.Down(stopwatch.Elapsed, e.Message);
        }
    }
}
