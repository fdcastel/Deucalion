using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Yaml;

internal class InterpolatedStringConverter : IYamlTypeConverter
{
    private string _lastKey = string.Empty;

    public bool Accepts(Type type) =>
        type == typeof(string) ||
        type == typeof(Uri);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.Current is Scalar sc)
        {
            if (sc.IsKey)
            {
                _lastKey = sc.Value;
                return parser.Consume<Scalar>().Value;
            }
        }

        var raw = parser.Consume<Scalar>().Value;
        var interpolated = raw.Replace("${MONITOR_NAME}", _lastKey, StringComparison.InvariantCultureIgnoreCase);

        return type == typeof(Uri)
            ? new Uri(interpolated)
            : interpolated;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        // Not used
    }
}
