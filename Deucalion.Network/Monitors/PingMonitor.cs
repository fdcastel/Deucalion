using System.Net.NetworkInformation;
using Deucalion.Monitors;
using Deucalion.Network.Monitors.Options;

namespace Deucalion.Network.Monitors
{
    public class PingMonitor : IPullMonitor<PingMonitorOptions>
    {
        private static readonly TimeSpan DefaultPingTimeout = TimeSpan.FromSeconds(1);

        public required PingMonitorOptions Options { get; init; }

        public async Task<MonitorState> QueryAsync()
        {
            try
            {
                using Ping pinger = new();
                var timeout = (Options.Timeout ?? DefaultPingTimeout).TotalMilliseconds;
                var reply = await pinger.SendPingAsync(Options.Host, (int)timeout);
                return MonitorState.FromBool(reply.Status == IPStatus.Success);
            }
            catch (PingException)
            {
                return MonitorState.Unknown;
            }
        }
    }
}
