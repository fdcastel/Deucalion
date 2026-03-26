using System.Net;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Yaml;

internal class IPEndPointConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(IPEndPoint);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var value = parser.Consume<Scalar>().Value;
        return IPEndPoint.Parse(value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        // Not used
    }
}
