using System;
using Newtonsoft.Json;

namespace Serious.Abbot.Serialization;

/// <summary>
/// Simple converter that uses the specified concrete type when deserializing for the target property.
/// </summary>
/// <typeparam name="TImplementation">The concrete type to use when deserializing.</typeparam>
/// <typeparam name="TInterface">The interface type that should match this converter.</typeparam>
public class ConcreteTypeConverter<TInterface, TImplementation> : JsonConverter
    where TImplementation : TInterface
{
    // Use the default writing behavior.
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
        throw new NotImplementedException();

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer) =>
        serializer.Deserialize<TImplementation>(reader);

    public override bool CanConvert(Type objectType) =>
        objectType == typeof(TInterface);
}
