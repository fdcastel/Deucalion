using SharpYaml.Serialization;

namespace Deucalion.Application.Yaml;

internal class UriConverter : YamlConverter<Uri>
{
    public override Uri? Read(YamlReader reader)
    {
        var value = reader.ScalarValue;
        reader.Read();
        return value is null ? null : new Uri(value, UriKind.RelativeOrAbsolute);
    }

    public override void Write(YamlWriter writer, Uri value)
        => writer.WriteScalar(value.OriginalString);
}
