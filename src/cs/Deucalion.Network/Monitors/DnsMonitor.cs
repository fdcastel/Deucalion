using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using Deucalion.Monitors;
using DnsClient;

namespace Deucalion.Network.Monitors;

public class DnsMonitor : PullMonitor
{
    public static readonly TimeSpan DefaultDnsTimeout = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultDnsWarnTimeout = TimeSpan.FromMilliseconds(500);

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

    public override async Task<MonitorResponse> QueryAsync()
    {
        LookupClientOptions options = Resolver is not null
            ? new(Resolver)
            : new();

        options.Timeout = Timeout!.Value;

        LookupClient lookup = new(options);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await lookup.QueryAsync(Host, RecordType ?? DefaultRecordType);
            stopwatch.Stop();

            return result.HasError
                ? MonitorResponse.Down(stopwatch.Elapsed, result.ErrorMessage)
                : MonitorResponse.Up(elapsed: stopwatch.Elapsed,
                                     text: result.Answers[0].ToString(),
                                     warnElapsed: WarnTimeout);
        }
        catch (DnsResponseException e)
        {
            return MonitorResponse.Down(stopwatch.Elapsed, e.Message);
        }
    }
}
