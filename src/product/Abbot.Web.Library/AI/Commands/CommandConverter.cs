using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Slack.Abstractions;

[assembly: InternalsVisibleTo("Abbot.Web.Library.Tests")]

namespace Serious.Abbot.AI.Commands;

/// <summary>
/// Converts incoming AI command elements to the corresponding <see cref="Command"/> type.
/// </summary>
/// <remarks>
/// Makes use of the <see cref="CommandAttribute"/> applied to a class to help determine how to deserialize it.
/// </remarks>
#pragma warning disable CA1812
class CommandConverter : JsonConverter<Command>
#pragma warning restore
{
    readonly CommandRegistry _registry;

    public CommandConverter(CommandRegistry registry)
    {
        _registry = registry;
    }

    public override void WriteJson(JsonWriter writer, Command? value, JsonSerializer serializer)
    {
        // We don't write. We use the default serialization for writing.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Use default behavior for writing.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// Deserialize the incoming JSON to the corresponding <see cref="Command"/> type.
    /// </summary>
    /// <param name="reader">The <see cref="JsonReader"/>.</param>
    /// <param name="objectType">The target type.</param>
    /// <param name="existingValue">Any existing value. We ignore this.</param>
    /// <param name="hasExistingValue">Whether there is an existing value.</param>
    /// <param name="serializer">The <see cref="JsonSerializer"/> to use.</param>
    public override Command? ReadJson(
        JsonReader reader,
        Type objectType,
        Command? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.StartObject:
                return ReadObject(reader, serializer);
            case JsonToken.Null:
                return default;
            default:
                throw new InvalidOperationException(
                    $"Could not read {objectType} from {nameof(JsonToken)}.{reader.TokenType}.");
        }
    }

    Command? ReadObject(JsonReader reader, JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);
        var name = (string?)jsonObject["command"];
        if (name is not { Length: > 0 })
        {
            return null;
        }

        var instance = _registry.TryResolveCommand(name, out var descriptor)
            ? Activator.CreateInstance(descriptor.Type)
            : new UnknownCommand(name);
        if (instance is null)
        {
            return null;
        }

        // Use populate to prevent a recursive call into the serializer.
        serializer.Populate(jsonObject.CreateReader(), instance);
        return (Command)instance;
    }
}
