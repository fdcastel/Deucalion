using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class TcpMonitor : PullMonitor
{
    private static readonly TimeSpan DefaultTcpTimeout = TimeSpan.FromMilliseconds(500);

    [Required]
    public string Host { get; set; } = default!;
    [Required]
    public int Port { get; set; } = default!;

    public override async Task<MonitorResponse> QueryAsync()
    {
        try
        {
            using TcpClient tcpClient = new();
            using CancellationTokenSource cts = new(Timeout ?? DefaultTcpTimeout);
            await tcpClient.ConnectAsync(Host, Port, cts.Token);
            return MonitorResponse.DefaultUp;
        }
        catch (SocketException)
        {
            return MonitorResponse.DefaultDown;
        }
        catch (OperationCanceledException)
        {
            return MonitorResponse.DefaultDown;
        }
    }
}
