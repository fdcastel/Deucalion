namespace Deucalion.Monitors.Options;

public class MonitorOptions
{
    public static readonly int DefaultIgnoreFailCount = 0;
    public static readonly bool DefaultUpsideDown = false;

    public string Name { get; set; } = default!;

    public int? IgnoreFailCount { get; set; }
    public bool? UpsideDown { get; set; }

    public int IgnoreFailCountOrDefault => IgnoreFailCount ?? DefaultIgnoreFailCount;
    public bool UpsideDownOrDefault => UpsideDown ?? DefaultUpsideDown;
}
