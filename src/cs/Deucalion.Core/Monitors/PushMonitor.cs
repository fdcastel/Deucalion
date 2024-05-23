﻿namespace Deucalion.Monitors;

public abstract class PushMonitor : MonitorBase
{
    public static readonly TimeSpan DefaultIntervalToDown = TimeSpan.FromSeconds(60);

    public TimeSpan IntervalToDown { get; set; } = DefaultIntervalToDown;

    public abstract void CheckIn(MonitorResponse? response = null);
}
