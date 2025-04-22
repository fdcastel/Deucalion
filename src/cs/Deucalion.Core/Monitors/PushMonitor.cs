namespace Deucalion.Monitors; // Corrected namespace

public abstract class PushMonitor : Monitor
{
    public static readonly TimeSpan DefaultIntervalToDown = TimeSpan.FromSeconds(60);

    public TimeSpan IntervalToDown { get; set; } = DefaultIntervalToDown;

    public abstract void CheckIn(MonitorResponse? response = null);
}
