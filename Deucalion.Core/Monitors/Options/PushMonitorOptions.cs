namespace Deucalion.Monitors.Options
{
    public class PushMonitorOptions : MonitorOptions
    {
        public static readonly TimeSpan DefaultIntervalToDown = TimeSpan.FromSeconds(60);

        public TimeSpan? IntervalToDown { get; set; }

        public TimeSpan IntervalToDownOrDefault => IntervalToDown ?? DefaultIntervalToDown;
    }
}
