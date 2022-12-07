﻿using Deucalion.Monitors;
using Deucalion.Network.Monitors.Options;
using DnsClient;

namespace Deucalion.Network.Monitors
{
    public class DnsMonitor : IPullMonitor<DnsMonitorOptions>
    {
        private static readonly TimeSpan DefaultDnsTimeout = TimeSpan.FromMilliseconds(500);

        public required DnsMonitorOptions Options { get; init; }

        public async Task<MonitorState> QueryAsync()
        {
            try
            {
                LookupClientOptions options = Options.Resolver is not null
                    ? new(Options.Resolver)
                    : new();

                options.Timeout = Options.Timeout ?? DefaultDnsTimeout;

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