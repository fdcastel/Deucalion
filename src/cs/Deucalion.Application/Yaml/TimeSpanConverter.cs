using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Yaml;

internal class TimeSpanConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(TimeSpan) || type == typeof(TimeSpan?);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var value = parser.Consume<Scalar>().Value;
        return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        // Not used
    }
}
