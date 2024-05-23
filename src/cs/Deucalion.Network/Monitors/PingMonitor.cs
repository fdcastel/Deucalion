using System.Net.NetworkInformation;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class PingMonitor : PullMonitor
{
    public static readonly TimeSpan DefaultPingTimeout = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultPingWarnTimeout = TimeSpan.FromMilliseconds(500);

    public required string Host { get; set; }

    public PingMonitor()
    {
        Timeout = DefaultPingTimeout;
        WarnTimeout = DefaultPingWarnTimeout;
    }

    public override async Task<MonitorResponse> QueryAsync()
    {
        try
        {
            using Ping pinger = new();
            var reply = await pinger.SendPingAsync(Host, (int)Timeout.TotalMilliseconds);
            var elapsed = TimeSpan.FromMilliseconds(reply.RoundtripTime);

            return reply.Status == IPStatus.Success
                ? MonitorResponse.Up(elapsed, warnElapsed: WarnTimeout)
                : MonitorResponse.Down(elapsed, text: reply.Status.ToString());
        }
        catch (PingException e)
        {
            return MonitorResponse.Down(null, e.Message);
        }
    }
}
