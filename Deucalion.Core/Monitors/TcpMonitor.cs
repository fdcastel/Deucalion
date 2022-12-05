using System.Net.Sockets;
using Deucalion.Monitors.Options;

namespace Deucalion.Monitors
{
    public class TcpMonitor : IMonitor<TcpMonitorOptions>
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(500);

        public required TcpMonitorOptions Options { get; init; }

        public async Task<bool> IsUpAsync()
        {
            try
            {
                using TcpClient tcpClient = new();
                using CancellationTokenSource cts = new(Options.Timeout ?? DefaultTimeout);
                await tcpClient.ConnectAsync(Options.Host, Options.Port, cts.Token);
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
