﻿using Deucalion.Monitors.Options;
using System.Net.Sockets;

namespace Deucalion.Monitors
{
    public class TcpMonitor : IMonitor
    {
        private static readonly int DefaultTimeout = 500;

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
