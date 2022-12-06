using System.Net.NetworkInformation;
using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public class PingMonitor : IMonitor<PingMonitorOptions>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        public required PingMonitorOptions Options { get; init; }

        public async Task<MonitorState> QueryAsync()
        {
            try
            {
                using Ping pinger = new();
                var timeout = (Options.Timeout ?? DefaultTimeout).TotalMilliseconds;
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
