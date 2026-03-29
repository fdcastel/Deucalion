using SharpYaml;
using SharpYaml.Serialization;

namespace Deucalion.Configuration;

[YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag, UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase)]
public record PullMonitorConfiguration
{
    public string? Name { get; set; }
    public string? Group { get; set; }
    public string? Href { get; set; }
    public string? Image { get; set; }
    public string[]? Tags { get; set; }

    public int? IgnoreFailCount { get; set; }
    public bool? UpsideDown { get; set; }

    public TimeSpan? IntervalWhenUp { get; set; }
    public TimeSpan? IntervalWhenDown { get; set; }

    public TimeSpan? Timeout { get; set; }
    public TimeSpan? WarnTimeout { get; set; }
}
