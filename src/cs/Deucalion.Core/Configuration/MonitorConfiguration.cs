namespace Deucalion.Configuration;

public record MonitorConfiguration
{
    public string? Name { get; set; }
    public string? Group { get; set; }
    public string? Href { get; set; }
    public string? Image { get; set; }

    public int? IgnoreFailCount { get; set; }
    public bool? UpsideDown { get; set; }
}
