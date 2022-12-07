namespace Deucalion.Monitors.Options
{
    public class PullMonitorOptions : MonitorOptions
    {
        public static readonly TimeSpan DefaultIntervalWhenUp = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan DefaultIntervalWhenDown = TimeSpan.FromMinutes(1);

        public TimeSpan? IntervalWhenUp { get; set; }
        public TimeSpan? IntervalWhenDown { get; set; }
        public TimeSpan? Timeout { get; set; }

        public TimeSpan IntervalWhenUpOrDefault => IntervalWhenUp ?? DefaultIntervalWhenUp;
        public TimeSpan IntervalWhenDownOrDefault => IntervalWhenDown ?? DefaultIntervalWhenDown;
    }
}
