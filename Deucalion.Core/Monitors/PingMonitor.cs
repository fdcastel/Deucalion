using Deucalion.Monitors.Options;
using System.Net.NetworkInformation;

namespace Deucalion.Monitors
{
    public class PingMonitor : IMonitor
    {
        private static readonly int DefaultTimeout = 1000;

        public required PingMonitorOptions Options { get; init; }

        public async Task<bool> IsUpAsync()
        {
            try
            {
                using Ping pinger = new();
                PingReply reply = await pinger.SendPingAsync(Options.Host, Options.Timeout ?? DefaultTimeout);
                return reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                return false;
            }
        }
    }
}
