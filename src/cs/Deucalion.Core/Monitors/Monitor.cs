namespace Deucalion.Monitors;

public abstract class Monitor
{
    public string Name { get; set; } = string.Empty;

    public int IgnoreFailCount { get; set; }
    public bool UpsideDown { get; set; }
}
