using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Yaml;
internal class HttpMethodConverter(bool doubleQuotes = false) : IYamlTypeConverter
{
    private readonly bool _doubleQuotes = doubleQuotes;

    public bool Accepts(Type type)
    {
        return type == typeof(HttpMethod);
    }

    public object ReadYaml(IParser parser, Type type) =>
        new HttpMethod(parser.Consume<Scalar>().Value);

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var m = (HttpMethod)value!;
        var formatted = m.ToString();
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, formatted, _doubleQuotes ? ScalarStyle.DoubleQuoted : ScalarStyle.Any, true, false));
    }
}
