using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Serialization;

/// <summary>
/// Defines the Abbot JSON formatting.
/// </summary>
/// <remarks>
/// <para>
/// When doing any serialization/deserialization between skill runners and Abbot's APIs, use this type.
/// This ensures that we are always using consistent serialization settings.
/// </para>
/// <para>
/// This type is abstract so that it can be implemented using both Newtonsoft.Json and (eventually) System.Text.Json,
/// to allow for migration away from JSON.NET as desired.
/// </para>
/// </remarks>
public abstract class AbbotJsonFormat
{
    /// <summary>
    /// The default Abbot JSON format service.
    /// </summary>
    public static AbbotJsonFormat Default => NewtonsoftJson;

    /// <summary>
    /// An Abbot JSON format service specific to Newtonsoft.Json.
    /// Use this when you absolutely positively HAVE to use Newtonsoft.Json and can accept no substitutes.
    /// </summary>
    public static readonly NewtonsoftJsonAbbotJsonFormat NewtonsoftJson = new();

    /// <summary>
    /// Deserializes the JSON to the specified .NET type.
    /// </summary>
    /// <param name="value">The JSON to deserialize.</param>
    // ReSharper disable once UseNullableAnnotationInsteadOfAttribute
    [return: MaybeNull]
    public abstract T? Deserialize<T>(string value);

    /// <summary>
    /// Deserializes the JSON to a .NET object using <see cref="JsonSerializerSettings"/>.
    /// </summary>
    /// <param name="value">The JSON to deserialize.</param>
    /// <returns>The deserialized object from the JSON string.</returns>
    public abstract object? Deserialize(string value);

    /// <summary>
    /// Converts the specified value, as deserialized by AbbotJsonFormat's default deserialization for <see cref="System.Object" /> properties, into <typeparamref name="T"/>
    /// </summary>
    /// <param name="value">The value deserialized in a <see cref="System.Object"/> property or dictionary value.</param>
    /// <returns>The value, converted to <typeparamref name="T"/>, or <c>default</c> if the input was null.</returns>
    /// <typeparam name="T">The target type.</typeparam>
    public abstract T? Convert<T>(object? value) where T : notnull;

    /// <summary>
    /// Serializes the specified object to clean JSON string using formatting.
    /// </summary>
    /// <param name="value">The object to serialize.</param>
    /// <param name="writeIndented">Indicates if the JSON should be indented or not.</param>
    /// <returns>
    /// A JSON string representation of the object.
    /// </returns>
    public abstract string Serialize(object? value, bool writeIndented = false);
}

public class NewtonsoftJsonAbbotJsonFormat : AbbotJsonFormat
{
    [Obsolete("Use AbbotJsonFormat.NewtonsoftJson")]
    public static new NewtonsoftJsonAbbotJsonFormat Default => NewtonsoftJson;

    /// <summary>
    /// Constructs a <see cref="NewtonsoftJsonAbbotJsonFormat"/> with required settings applied to <paramref name="serializerSettings"/>.
    /// </summary>
    /// <param name="serializerSettings">The initial <see cref="JsonSerializerSettings"/>.</param>
    public NewtonsoftJsonAbbotJsonFormat(JsonSerializerSettings? serializerSettings = null)
    {
        SerializerSettings = Apply(serializerSettings ?? new());
        Serializer = JsonSerializer.Create(serializerSettings);
    }

    public JsonSerializerSettings SerializerSettings { get; }
    public JsonSerializer Serializer { get; }

    // Using "?" syntax here doesn't work because the generic constraints are specified on the base method and can't be redefined here.
    // Not sure why C# doesn't seem to see the "where T: notnull" constraint on the base method, but it doesn't and thus thinks "T?" is invalid.
    // So we use the MaybeNullAttribute instead.
    [return: MaybeNull]
    public override T Deserialize<T>(string value)
    {
        return JsonConvert.DeserializeObject<T>(value, SerializerSettings);
    }

    public override object? Deserialize(string value)
    {
        return JsonConvert.DeserializeObject(value, SerializerSettings);
    }

    public override T? Convert<T>(object? value)
        where T : default
    {
        switch (value)
        {
            case T t:
                return t;
            case JToken j:
                return JsonToObject<T>(j);
            case null:
                return default;
            default:
                return JsonToObject<T>(ObjectToJson(value));
        }
    }

    public override string Serialize(object? value, bool writeIndented = false)
    {
        return JsonConvert.SerializeObject(value,
            writeIndented
                ? Formatting.Indented
                : Formatting.None,
            SerializerSettings);
    }

    public JToken ObjectToJson(object value) =>
        JToken.FromObject(value, Serializer);

    public T? JsonToObject<T>(JToken json) =>
        json.ToObject<T>(Serializer);

    public static JsonSerializerSettings Apply(JsonSerializerSettings settings)
    {
        settings.TypeNameHandling = TypeNameHandling.None;
        settings.NullValueHandling = NullValueHandling.Ignore;
        settings.Converters = new List<JsonConverter>
            {
                new StringEnumConverter(),
                new ConcreteTypeConverter<IArgument, Argument>(),
                new ConcreteTypeConverter<ILocation, Location>(),
                new ConcreteTypeConverter<ICoordinate, Coordinate>(),
                new ConcreteTypeConverter<IRoom, PlatformRoom>(),
                new ConcreteTypeConverter<IChatUser, PlatformUser>(),
                new ConcreteTypeConverter<IResponseSettings, ResponseSettings>(),
                new ArgumentConverter(),
            };
        settings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        return settings;
    }
}
