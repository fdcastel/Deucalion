using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Yaml;

internal class HttpMethodConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(HttpMethod);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) =>
        new HttpMethod(parser.Consume<Scalar>().Value);

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        // Not used
    }
}
