using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Deucalion.Application.Yaml;

public static class YamlDotNetExtensions
{
    public static DeserializerBuilder WithValidation(this DeserializerBuilder builder) =>
        builder.WithNodeDeserializer(
            inner => new ValidationDeserializer(inner),
            selection => selection.InsteadOf<ObjectNodeDeserializer>()
        );
}
