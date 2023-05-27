﻿namespace Deucalion.Monitors;

public abstract class MonitorBase
{
    public static int DefaultIgnoreFailCount = 0;
    public static bool DefaultUpsideDown = false;

    public string Name { get; set; } = default!;

    public int? IgnoreFailCount { get; set; }
    public bool? UpsideDown { get; set; }

    public int IgnoreFailCountOrDefault => IgnoreFailCount ?? DefaultIgnoreFailCount;
    public bool UpsideDownOrDefault => UpsideDown ?? DefaultUpsideDown;
}
