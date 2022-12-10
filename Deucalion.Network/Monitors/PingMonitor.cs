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

            return reply.Status == IPStatus.Success
                ? new() { State = MonitorState.Up, ResponseTime = TimeSpan.FromMilliseconds(reply.RoundtripTime) }
                : MonitorResponse.DefaultDown;
        }
        catch (PingException)
        {
            return MonitorResponse.DefaultDown;
        }
    }
}
