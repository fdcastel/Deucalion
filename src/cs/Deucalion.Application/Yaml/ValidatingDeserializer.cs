using System.ComponentModel.DataAnnotations;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Deucalion.Application.Yaml;

/// <summary>
/// Deserializer to validate objects using their associated <see cref="ValidationAttribute" /> attributes.
/// </summary>
/// <remarks>
/// Source: https://github.com/aaubry/YamlDotNet/issues/202#issuecomment-830712803
/// </remarks>
internal class ValidationDeserializer(INodeDeserializer nodeDeserializer) : INodeDeserializer
{
    private readonly INodeDeserializer _nodeDeserializer = nodeDeserializer;

    public bool Deserialize(IParser parser, Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
    {
        if (_nodeDeserializer.Deserialize(parser, expectedType, nestedObjectDeserializer, out value) && value is not null)
        {
            var context = new ValidationContext(value, null, null);
            try
            {
                Validator.ValidateObject(value, context, true);
                return true;
            }
            catch (ValidationException e) when (parser.Current != null)
            {
                throw new YamlException(parser.Current.Start, parser.Current.End, e.Message);
            }
        }
        return false;
    }
}
