using System.ComponentModel.DataAnnotations;
using System.Net.NetworkInformation;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class PingMonitor : PullMonitor
{
    private static readonly TimeSpan DefaultPingTimeout = TimeSpan.FromSeconds(1);

    [Required]
    public string Host { get; set; } = default!;

    public override async Task<MonitorResponse> QueryAsync()
    {
        try
        {
            using Ping pinger = new();
            var timeout = (Timeout ?? DefaultPingTimeout).TotalMilliseconds;
            var reply = await pinger.SendPingAsync(Host, (int)timeout);
            var elapsed = TimeSpan.FromMilliseconds(reply.RoundtripTime);

            return reply.Status == IPStatus.Success
                ? MonitorResponse.Up(elapsed, warnElapsed: WarnTimeout)
                : MonitorResponse.Down(elapsed, reply.Status.ToString());
        }
        catch (PingException e)
        {
            return MonitorResponse.Down(null, e.Message);
        }
    }
}
