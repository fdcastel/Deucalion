namespace Deucalion.Configuration;

// Polymorphic over the YAML type tag (!ping, !tcp, ...). The derived types live in
// Deucalion.Network and the tag mapping in Deucalion.Application's DeucalionYamlContext, so
// this project stays free of any serializer dependency.
public record PullMonitorConfiguration
{
    public string? Name { get; set; }
    public string? Group { get; set; }
    public string? Href { get; set; }

    public int? IgnoreFailCount { get; set; }
    public bool? UpsideDown { get; set; }

    public TimeSpan? IntervalWhenUp { get; set; }
    public TimeSpan? IntervalWhenDown { get; set; }

    public TimeSpan? Timeout { get; set; }
    public TimeSpan? WarnTimeout { get; set; }
}
