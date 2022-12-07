namespace Deucalion.Monitors.Options
{
    public class TcpMonitorOptions : PullMonitorOptions
    {
        public string Host { get; set; } = default!;
        public ushort Port { get; set; } = default!;
    }
}
