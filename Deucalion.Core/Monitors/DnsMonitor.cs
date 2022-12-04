using Deucalion.Monitors.Options;
using DnsClient;

namespace Deucalion.Monitors
{
    public class DnsMonitor : IMonitor
    {
        private static readonly int DefaultTimeout = 500;

        public required DnsMonitorOptions Options { get; init; }

        public async Task<bool> IsUpAsync()
        {
            try
            {
                LookupClientOptions options = Options.Resolver is not null
                    ? new(Options.Resolver)
                    : new();

                options.Timeout = TimeSpan.FromMilliseconds(Options.Timeout ?? DefaultTimeout);

                LookupClient lookup = new(options);
                IDnsQueryResponse result = await lookup.QueryAsync(Options.HostName, Options.RecordType ?? QueryType.A);
                return result.Answers.Any();

            }
            catch (DnsResponseException)
            {
                return false;
            }
        }
    }
}
