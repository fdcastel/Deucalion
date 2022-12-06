namespace Deucalion.Monitors.Options
{
    public class CheckInMonitorOptions : MonitorOptions
    {
        public static readonly TimeSpan DefaultIntervalToDown = TimeSpan.FromSeconds(60);

        public TimeSpan? IntervalToDown { get; set; }

        public TimeSpan IntervalToDownOrDefault => IntervalToDown ?? DefaultIntervalToDown;
    }
}
