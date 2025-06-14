using System.Diagnostics;
using System.Net.Sockets;
using Deucalion.Monitors;

namespace Deucalion.Network.Monitors;

public class TcpMonitor : PullMonitor
{
    public required string Host { get; set; }
    public required int Port { get; set; }

    public override async Task<MonitorResponse> QueryAsync(CancellationToken cancellationToken = default)
    {
        using TcpClient tcpClient = new();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await tcpClient.ConnectAsync(Host, Port, timeoutCts.Token);

            return MonitorResponse.Up(stopwatch.Elapsed, warnElapsed: WarnTimeout);
        }
        catch (SocketException e)
        {
            return MonitorResponse.Down(stopwatch.Elapsed, e.Message);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            // Catch only if the cancellation was due to the timeout -- https://stackoverflow.com/a/67203842
            return MonitorResponse.Down(Timeout, "Timeout");
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
