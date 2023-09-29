using Newtonsoft.Json;

namespace Serious.Abbot.Storage;

/// <summary>
/// A serializer for the bot brain to store and retrieve data items for a skill.
/// </summary>
public interface IBrainSerializer
{
    /// <summary>
    /// Deserializes the JSON to a .NET object using custom <see cref="JsonSerializerSettings"/>.
    /// </summary>
    /// <param name="value">The JSON to deserialize.</param>
    /// <returns>The deserialized object from the JSON string.</returns>
    object? Deserialize(string value);

    /// <summary>
    /// Deserializes the JSON to the specified .NET type.
    /// </summary>
    /// <param name="value">The JSON to deserialize.</param>
    T? Deserialize<T>(string value);

    /// <summary>
    /// Serializes the specified object to a JSON string retaining the .NET type name.
    /// </summary>
    /// <param name="value">The object to serialize.</param>
    /// <param name="withTypes">When <c>true</c>, includes type information in the serialized output.</param>
    /// <returns>A JSON string representation of the object.</returns>
    string SerializeObject(object? value, bool withTypes = true);
}
