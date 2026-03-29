using SharpYaml.Serialization;

namespace Deucalion.Application.Yaml;

internal class HttpMethodConverter : YamlConverter<HttpMethod>
{
    public override HttpMethod? Read(YamlReader reader)
    {
        var value = reader.ScalarValue;
        reader.Read();
        return value is null ? null : new HttpMethod(value);
    }

    public override void Write(YamlWriter writer, HttpMethod value)
        => writer.WriteScalar(value.Method);
}
