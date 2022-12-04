namespace Deucalion.Monitors.Options
{
    public class CommonMonitorOptions
    {
        public static readonly int DefaultIntervalWhenUp = 60;
        public static readonly int DefaultIntervalWhenDown = 60;
        public static readonly int DefaultIgnoreFailCount = 0;
        public static readonly bool DefaultUpsideDown = false;

        public string Name { get; set; } = default!;
        public int? IntervalWhenUp { get; set; }
        public int? IntervalWhenDown { get; set; }
        public int? IgnoreFailCount { get; set; }
        public int? Timeout { get; set; }
        public bool? UpsideDown { get; set; }

        public int IntervalWhenUpOrDefault => IntervalWhenUp ?? DefaultIntervalWhenUp;
        public int IntervalWhenDownOrDefault => IntervalWhenDown ?? DefaultIntervalWhenDown;
        public int IgnoreFailCountOrDefault => IgnoreFailCount ?? DefaultIgnoreFailCount;
        public bool UpsideDownOrDefault => UpsideDown ?? DefaultUpsideDown;
    }
}
