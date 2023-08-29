using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Yaml;
internal class HttpMethodConverter : IYamlTypeConverter
{
    private readonly bool doubleQuotes;

    public HttpMethodConverter(bool doubleQuotes = false)
    {
        this.doubleQuotes = doubleQuotes;
    }

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
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, formatted, doubleQuotes ? ScalarStyle.DoubleQuoted : ScalarStyle.Any, true, false));
    }
}
