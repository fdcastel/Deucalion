﻿using Deucalion.Monitors.Options;
using DnsClient;

namespace Deucalion.Monitors
{
    public class DnsMonitor : IPullMonitor<DnsMonitorOptions>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(500);

        public required DnsMonitorOptions Options { get; init; }

        public async Task<MonitorState> QueryAsync()
        {
            try
            {
                LookupClientOptions options = Options.Resolver is not null
                    ? new(Options.Resolver)
                    : new();

                options.Timeout = Options.Timeout ?? DefaultTimeout;

                LookupClient lookup = new(options);
                var result = await lookup.QueryAsync(Options.HostName, Options.RecordType ?? QueryType.A);

                return MonitorState.FromBool(result.Answers.Any());
            }
            catch (DnsResponseException)
            {
                return MonitorState.Down;
            }
        }
    }
}
