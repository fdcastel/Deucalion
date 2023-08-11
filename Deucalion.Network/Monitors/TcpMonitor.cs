using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
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
        using TcpClient tcpClient = new();
        var timeout = Timeout ?? DefaultTcpTimeout;
        using CancellationTokenSource cts = new(timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await tcpClient.ConnectAsync(Host, Port, cts.Token);
            stopwatch.Stop();

            return MonitorResponse.Up(elapsed: stopwatch.Elapsed, warnElapsed: WarnTimeout);
        }
        catch (SocketException e)
        {
            return MonitorResponse.Down(stopwatch.Elapsed, e.Message);
        }
        catch (OperationCanceledException)
        {
            return MonitorResponse.Down(timeout, "Timeout");
        }
    }
}
