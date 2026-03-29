using System.Net;
using SharpYaml.Serialization;

namespace Deucalion.Application.Yaml;

internal class IPEndPointConverter : YamlConverter<IPEndPoint>
{
    public override IPEndPoint? Read(YamlReader reader)
    {
        var value = reader.ScalarValue;
        reader.Read();
        return value is null ? null : IPEndPoint.Parse(value);
    }

    public override void Write(YamlWriter writer, IPEndPoint value)
        => writer.WriteScalar(value.ToString());
}
