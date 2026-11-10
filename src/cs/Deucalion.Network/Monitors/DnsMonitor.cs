using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class DnsMonitor : PullMonitor
{
    public static readonly TimeSpan DefaultDnsTimeout = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultDnsWarnTimeout = TimeSpan.FromMilliseconds(500);

    public static readonly DnsRecordType DefaultDnsRecordType = DnsRecordType.A;

    public required string Host { get; set; }

    public DnsRecordType RecordType { get; set; } = DefaultDnsRecordType;

    private IPEndPoint? _resolver;
    public IPEndPoint? Resolver
    {
        get => _resolver;
        set
        {
            var resolver = value is not null && value.Port == 0
                ? new IPEndPoint(value.Address, 53)
                : value;

            // On Windows DnsResolver goes through the OS DNS API, which only queries port 53 and
            // throws on anything else. Fail when the monitor is built, not on every probe.
            if (resolver is not null && resolver.Port != 53 && OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException($"DNS resolver '{resolver}': a port other than 53 is not supported on Windows.");
            }

            _resolver = resolver;
        }
    }

    public DnsMonitor()
    {
        // Dns has stricter defaults than PullMonitor
        Timeout = DefaultDnsTimeout;
    }

    public override TimeSpan TypeDefaultWarnTimeout => DefaultDnsWarnTimeout;

    public override async Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default)
    {
        using var resolver = Resolver is not null
            ? new DnsResolver(new DnsResolverOptions { Servers = [Resolver] })
            : new DnsResolver();

        // DnsResolverOptions has no timeout of its own.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (responseCode, answer) = await ResolveAsync(resolver, timeoutCts.Token);

            // Freezes stopwatch.Elapsed
            stopwatch.Stop();

            return responseCode == DnsResponseCode.NoError
                ? MonitorResponse.Up(elapsed: stopwatch.Elapsed, text: answer, warnElapsed: EffectiveWarnTimeout)
                : MonitorResponse.Down(stopwatch.Elapsed, Describe(responseCode));
        }
        catch (SocketException e)
        {
            return MonitorResponse.Down(stopwatch.Elapsed, e.Message);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            // Catch only if the cancellation was due to the timeout -- https://stackoverflow.com/a/67203842
            return MonitorResponse.Down(stopwatch.Elapsed, "Timeout");
        }
    }

    private async Task<(DnsResponseCode ResponseCode, string? Answer)> ResolveAsync(DnsResolver resolver, CancellationToken cancellationToken) =>
        RecordType switch
        {
            DnsRecordType.A => Summarize(await resolver.ResolveAddressesAsync(Host, AddressFamily.InterNetwork, cancellationToken), static r => (r.Ttl, r.Address.ToString())),
            DnsRecordType.AAAA => Summarize(await resolver.ResolveAddressesAsync(Host, AddressFamily.InterNetworkV6, cancellationToken), static r => (r.Ttl, r.Address.ToString())),
            DnsRecordType.CNAME => Summarize(await resolver.ResolveCNameAsync(Host, cancellationToken), static r => (r.Ttl, r.CanonicalName)),
            DnsRecordType.MX => Summarize(await resolver.ResolveMxAsync(Host, cancellationToken), static r => (r.Ttl, $"{r.Preference} {r.Exchange}")),
            DnsRecordType.NS => Summarize(await resolver.ResolveNsAsync(Host, cancellationToken), static r => (r.Ttl, r.Name)),
            // A literal address is looked up in its reverse zone (in-addr.arpa / ip6.arpa).
            DnsRecordType.PTR => Summarize(IPAddress.TryParse(Host, out var address)
                ? await resolver.ResolvePtrAsync(address, cancellationToken)
                : await resolver.ResolvePtrAsync(Host, cancellationToken), static r => (r.Ttl, r.Name)),
            DnsRecordType.SRV => Summarize(await resolver.ResolveSrvAsync(Host, cancellationToken), static r => (r.Ttl, $"{r.Priority} {r.Weight} {r.Port} {r.Target}")),
            DnsRecordType.TXT => Summarize(await resolver.ResolveTxtAsync(Host, cancellationToken), static r => (r.Ttl, string.Join(' ', r.Values.Select(static v => $"\"{v}\"")))),
            _ => throw new InvalidOperationException($"Unsupported DNS record type '{RecordType}'."),
        };

    /// <summary>
    /// The response code, plus the first record in zone-file form
    /// (<c>google.com. 300 IN A 142.250.79.46</c>) when there is one.
    /// </summary>
    private (DnsResponseCode, string?) Summarize<T>(DnsResult<T> result, Func<T, (TimeSpan Ttl, string Data)> describe)
    {
        if (result.Records.Count == 0)
        {
            return (result.ResponseCode, null);
        }

        var (ttl, data) = describe(result.Records[0]);
        return (result.ResponseCode, $"{Host.TrimEnd('.')}. {(long)ttl.TotalSeconds} IN {RecordType} {data}");
    }

    internal static string Describe(DnsResponseCode responseCode) => responseCode switch
    {
        DnsResponseCode.FormatError => "Format Error",
        DnsResponseCode.ServerFailure => "Server Failure",
        DnsResponseCode.NxDomain => "Non-Existent Domain",
        DnsResponseCode.NotImplemented => "Not Implemented",
        DnsResponseCode.Refused => "Query Refused",
        _ => $"DNS error {(int)responseCode}",
    };
}
