namespace Deucalion.Monitors;

public abstract class MonitorBase
{
    public static int DefaultIgnoreFailCount = 0;
    public static bool DefaultUpsideDown = false;

    public string Name { get; set; } = default!;
    public string? Group { get; set; }
    public string? Href { get; set; }
    public string? Image { get; set; }

    public int? IgnoreFailCount { get; set; }
    public bool? UpsideDown { get; set; }
}
