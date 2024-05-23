using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Yaml;

internal class InterpolatedStringConverter : IYamlTypeConverter
{
    // Not thread-safe
    internal static string LastKey = string.Empty;

    public bool Accepts(Type type) =>
        type == typeof(string) ||
        type == typeof(Uri);

    public object ReadYaml(IParser parser, Type type)
    {
        if (parser.Current is Scalar sc)
        {
            if (sc.IsKey)
            {
                LastKey = sc.Value;
                return parser.Consume<Scalar>().Value;
            }
        }

        var raw = parser.Consume<Scalar>().Value;
        var interpolated = raw.Replace("${MONITOR_NAME}", LastKey, StringComparison.InvariantCultureIgnoreCase);

        return type == typeof(Uri)
            ? new Uri(interpolated)
            : interpolated;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        // Not used
    }
}
