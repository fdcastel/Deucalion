using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Deucalion.Application.Yaml;

internal static class YamlDotNetExtensions
{
    internal static StaticDeserializerBuilder WithValidation(this StaticDeserializerBuilder builder) =>
        builder.WithNodeDeserializer(
            inner => new ValidationDeserializer(inner),
            selection => selection.InsteadOf<ObjectNodeDeserializer>()
        );
}
