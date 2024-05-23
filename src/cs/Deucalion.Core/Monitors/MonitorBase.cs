namespace Deucalion.Monitors;

// ToDo: Rename to Monitor
public abstract class MonitorBase
{
    public string Name { get; set; } = string.Empty;

    public int IgnoreFailCount { get; set; }
    public bool UpsideDown { get; set; }
}
