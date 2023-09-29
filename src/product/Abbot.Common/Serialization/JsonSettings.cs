namespace Serious.Abbot.Serialization;

/// <summary>
/// Base class for types that serve as settings or metadata stored as a JSON string.
/// </summary>
public abstract record JsonSettings
{
    /// <summary>
    /// The default <see cref="AbbotJsonFormat"/> to use for serialization and deserialization
    /// </summary>
    /// <remarks>Uses <see cref="AbbotJsonFormat.Default"/>.</remarks>
    public static AbbotJsonFormat DefaultJsonFormat => AbbotJsonFormat.Default;

    /// <summary>
    /// Deserializes JSON into a settings type.
    /// </summary>
    /// <remarks>Uses <see cref="DefaultJsonFormat"/>.</remarks>
    /// <param name="json">The JSON string.</param>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    public static T? FromJson<T>(string? json) where T : JsonSettings =>
        json is null ? default : DefaultJsonFormat.Deserialize<T>(json);

    /// <summary>
    /// Serializes this object to JSON.
    /// </summary>
    /// <remarks>Uses <see cref="DefaultJsonFormat"/>.</remarks>
    /// <param name="writeIndented">
    /// Indicates if the JSON should be indented or not.
    /// Default: <see langword="false"/>.
    /// </param>
    /// <returns>A JSON string representation of this object.</returns>
    public string ToJson(bool writeIndented = false) =>
        DefaultJsonFormat.Serialize(this, writeIndented);
}
