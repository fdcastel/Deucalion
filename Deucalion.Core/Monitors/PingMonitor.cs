using Deucalion.Monitors.Options;
using System.Net.NetworkInformation;

namespace Deucalion.Monitors
{
    public class PingMonitor : IMonitor<PingMonitorOptions>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        public required PingMonitorOptions Options { get; init; }

        public async Task<bool> IsUpAsync()
        {
            try
            {
                using Ping pinger = new();
                double timeout = (Options.Timeout ?? DefaultTimeout).TotalMilliseconds;
                PingReply reply = await pinger.SendPingAsync(Options.Host, (int)timeout);
                return reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                return false;
            }
        }
    }
}
