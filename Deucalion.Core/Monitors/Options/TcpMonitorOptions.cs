namespace Deucalion.Monitors.Options
{
    public class TcpMonitorOptions : CommonMonitorOptions
    {
        public string Host { get; set; } = default!;
        public ushort Port { get; set; } = default!;
    }
}
