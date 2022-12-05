namespace Deucalion.Monitors.Options
{
    public class MonitorOptions
    {
        public static readonly TimeSpan DefaultIntervalWhenUp = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan DefaultIntervalWhenDown = TimeSpan.FromMinutes(1);
        public static readonly int DefaultIgnoreFailCount = 0;
        public static readonly bool DefaultUpsideDown = false;

        public string Name { get; set; } = default!;
        public TimeSpan? IntervalWhenUp { get; set; }
        public TimeSpan? IntervalWhenDown { get; set; }
        public int? IgnoreFailCount { get; set; }
        public TimeSpan? Timeout { get; set; }
        public bool? UpsideDown { get; set; }

        public TimeSpan IntervalWhenUpOrDefault => IntervalWhenUp ?? DefaultIntervalWhenUp;
        public TimeSpan IntervalWhenDownOrDefault => IntervalWhenDown ?? DefaultIntervalWhenDown;
        public int IgnoreFailCountOrDefault => IgnoreFailCount ?? DefaultIgnoreFailCount;
        public bool UpsideDownOrDefault => UpsideDown ?? DefaultUpsideDown;
    }
}
