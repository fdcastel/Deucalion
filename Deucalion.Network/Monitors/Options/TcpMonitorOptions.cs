﻿using Deucalion.Monitors.Options;

namespace Deucalion.Network.Monitors.Options
{
    public class TcpMonitorOptions : PullMonitorOptions
    {
        public string Host { get; set; } = default!;
        public ushort Port { get; set; } = default!;
    }
}