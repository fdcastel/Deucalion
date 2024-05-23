using System.Diagnostics;
using System.Net.Sockets;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class TcpMonitor : PullMonitor
{
    public required string Host { get; set; }
    public required int Port { get; set; }

    public override async Task<MonitorResponse> QueryAsync()
    {
        using TcpClient tcpClient = new();
        using CancellationTokenSource cts = new(Timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await tcpClient.ConnectAsync(Host, Port, cts.Token);
            stopwatch.Stop();

            return MonitorResponse.Up(stopwatch.Elapsed, warnElapsed: WarnTimeout);
        }
        catch (SocketException e)
        {
            return MonitorResponse.Down(stopwatch.Elapsed, e.Message);
        }
        catch (OperationCanceledException)
        {
            return MonitorResponse.Down(Timeout, "Timeout");
        }
    }
}
