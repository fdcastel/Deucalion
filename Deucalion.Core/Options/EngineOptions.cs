namespace Deucalion.Options
{
    public class EngineOptions
    {
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(1);

        public TimeSpan? Interval { get; set; }

        public TimeSpan IntervalOrDefault => Interval ?? DefaultInterval;
    }
}