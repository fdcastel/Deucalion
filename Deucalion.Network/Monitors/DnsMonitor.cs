using Deucalion.Monitors;
using Deucalion.Network.Monitors.Options;
using DnsClient;

namespace Deucalion.Network.Monitors;

public class DnsMonitor : IPullMonitor<DnsMonitorOptions>
{
    private static readonly TimeSpan DefaultDnsTimeout = TimeSpan.FromMilliseconds(500);

    public required DnsMonitorOptions Options { get; init; }

    public async Task<MonitorResponse> QueryAsync()
    {
        try
        {
            LookupClientOptions options = Options.Resolver is not null
                ? new(Options.Resolver)
                : new();

            options.Timeout = Options.Timeout ?? DefaultDnsTimeout;

            LookupClient lookup = new(options);
            var result = await lookup.QueryAsync(Options.Host, Options.RecordType ?? QueryType.A);

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
