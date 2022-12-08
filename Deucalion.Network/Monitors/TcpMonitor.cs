using System.Net.Sockets;
using Deucalion.Monitors;
using Deucalion.Network.Monitors.Options;

namespace Deucalion.Network.Monitors
{
    public class TcpMonitor : IPullMonitor<TcpMonitorOptions>
    {
        private static readonly TimeSpan DefaultTcpTimeout = TimeSpan.FromMilliseconds(500);

        public required TcpMonitorOptions Options { get; init; }

        public async Task<MonitorResponse> QueryAsync()
        {
            try
            {
                using TcpClient tcpClient = new();
                using CancellationTokenSource cts = new(Options.Timeout ?? DefaultTcpTimeout);
                await tcpClient.ConnectAsync(Options.Host, Options.Port, cts.Token);
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
}
