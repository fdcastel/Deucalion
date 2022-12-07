using System.Net.Sockets;
using Deucalion.Monitors;
using Deucalion.Network.Monitors.Options;

namespace Deucalion.Network.Monitors
{
    public class TcpMonitor : IPullMonitor<TcpMonitorOptions>
    {
        private static readonly TimeSpan DefaultTcpTimeout = TimeSpan.FromMilliseconds(500);

        public required TcpMonitorOptions Options { get; init; }

        public async Task<MonitorState> QueryAsync()
        {
            try
            {
                using TcpClient tcpClient = new();
                using CancellationTokenSource cts = new(Options.Timeout ?? DefaultTcpTimeout);
                await tcpClient.ConnectAsync(Options.Host, Options.Port, cts.Token);
                return MonitorState.Up;
            }
            catch (SocketException)
            {
                return MonitorState.Down;
            }
            catch (OperationCanceledException)
            {
                return MonitorState.Down;
            }
        }
    }
}
