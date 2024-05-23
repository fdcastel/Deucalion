using System.Diagnostics;
using System.Net;
using Deucalion.Monitors;
using DnsClient;

namespace Deucalion.Network.Monitors;

public class DnsMonitor : PullMonitor
{
    public static readonly TimeSpan DefaultDnsTimeout = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultDnsWarnTimeout = TimeSpan.FromMilliseconds(500);

    public static readonly QueryType DefaultDnsRecordType = QueryType.A;

    public required string Host { get; set; }

    public QueryType RecordType { get; set; } = DefaultDnsRecordType;

    private IPEndPoint? _resolver;
    public IPEndPoint? Resolver
    {
        get => _resolver;
        set => _resolver = value is not null
            ? value.Port == 0
                ? new IPEndPoint(value.Address, 53)
                : value
            : null;
    }

    public DnsMonitor()
    {
        Timeout = DefaultDnsTimeout;
        WarnTimeout = DefaultDnsWarnTimeout;
    }

    public override async Task<MonitorResponse> QueryAsync()
    {
        LookupClientOptions options = Resolver is not null
            ? new(Resolver)
            : new();

        options.Timeout = Timeout;

        LookupClient lookup = new(options);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await lookup.QueryAsync(Host, RecordType);
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
