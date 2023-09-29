using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

[assembly: InternalsVisibleTo("Abbot.Web.Library.Tests")]

namespace Serious.Slack.Converters;

/// <summary>
/// Converts incoming Slack elements to the corresponding <see cref="IElement"/> type.
/// </summary>
/// <remarks>
/// Makes use of the <see cref="ElementAttribute"/> applied to a class to help determine how to deserialize it.
/// </remarks>
#pragma warning disable CA1812
class ElementConverter : JsonConverter<IElement>
#pragma warning restore
{
    // Map of the slack element type value to the .NET type to deserialize to.
    static readonly IReadOnlyDictionary<string, Mapping> ElementMap = CreateElementMap();

    public override void WriteJson(JsonWriter writer, IElement? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Use default behavior for writing.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// Deserialize the incoming JSON to the corresponding <see cref="IElement"/> type.
    /// </summary>
    /// <param name="reader">The <see cref="JsonReader"/>.</param>
    /// <param name="objectType">The target type.</param>
    /// <param name="existingValue">Any existing value. We ignore this.</param>
    /// <param name="hasExistingValue">Whether there is an existing value.</param>
    /// <param name="serializer">The <see cref="JsonSerializer"/> to use.</param>
    public override IElement? ReadJson(
        JsonReader reader,
        Type objectType,
        IElement? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.None:
            case JsonToken.StartObject:
                return ReadObject(reader, objectType, serializer);

            case JsonToken.Null:
                return default;

            case JsonToken.String:
                var value = (string?)reader.Value
                    ?? throw new InvalidCastException(
                        "Expected Value for JsonToken.String to be a string; received null.");

                if (objectType.IsAssignableFrom(typeof(PlainText)))
                {
                    return new PlainText(value);
                }
                goto default;

            default:
                throw new InvalidOperationException(
                    $"Could not read {objectType} from {nameof(JsonToken)}.{reader.TokenType}.");
        }
    }

    IElement? ReadObject(JsonReader reader, Type objectType, JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);
        var type = (string?)jsonObject["type"];
        if (type is not { Length: > 0 })
        {
            return null;
        }

        // The type is a get only property of `IElement` so we don't need to set it.
        // If we don't remove it here, it'll be added to the IPropertyBag.AdditionalProperties collection.
        // Then serializing the object will have two `type` properties. The only time this is ok is
        // if we're deserializing an unknown type. Then we're going to want to know the original type.
        jsonObject.Remove("type");

        var clrType = GetMappedClrType(type, objectType, jsonObject);

        return clrType is not null
            ? PopulateInstance(type, clrType, jsonObject, serializer)
            : PopulateInstance(type, objectType, jsonObject, serializer);
    }

    static Type? GetMappedClrType(string type, Type objectType, JObject jsonObject)
    {
        if (ElementMap.TryGetValue(type, out var mapping)
            && mapping.GetClosestType(objectType, jsonObject) is { } matchingType)
        {
            return matchingType;
        }

        return null;
    }

    IElement PopulateInstance(string type, Type instanceType, JObject jsonObject, JsonSerializer serializer)
    {
        object? instance = null;
        if (instanceType.IsInterface || instanceType.IsAbstract)
        {
            if (instanceType.IsAssignableTo(typeof(IEventEnvelope<>)))
            {
                instanceType = typeof(EventEnvelope<>);
            }
            else if (instanceType.IsAssignableTo(typeof(IPayloadElement)))
            {
                instanceType = typeof(FallbackInteractiveElement);
                instance = new FallbackInteractiveElement(type);
            }
            else if (instanceType.IsAssignableTo(typeof(ILayoutBlock)))
            {
                instanceType = typeof(FallbackLayoutBlock);
                instance = new FallbackLayoutBlock(type);
            }
            else if (instanceType.IsAssignableTo(typeof(IElement)))
            {
                instanceType = typeof(FallbackElement);
                // Since we know the actual type, we can create the instance as an optimization.
                instance = new FallbackElement(type);
            }
        }

        if (instanceType.ContainsGenericParameters || instanceType.IsGenericType)
        {
            // Special case for generic types.
            var eventJsonObject = jsonObject["event"] as JObject;
            var eventType = eventJsonObject?["type"]?.Value<string>() ?? string.Empty;
            if (eventJsonObject is not null
                && GetMappedClrType(eventType, instanceType, eventJsonObject) is { } eventClrType)
            {
                var typeParams = new[] { eventClrType };
                instanceType = instanceType.MakeGenericType(typeParams);
            }
            else
            {
                instanceType = typeof(EventEnvelope<FallbackEventBody>);
                // Since we know the actual type, we can create the instance as an optimization.
                instance = new EventEnvelope<FallbackEventBody>
                {
                    Event = new FallbackEventBody(eventType)
                };
            }
        }

        var customConverter = instanceType.GetCustomAttribute<JsonConverterAttribute>();
        if (customConverter is not null && customConverter.ConverterType != GetType())
        {
            // The purpose of this converter is to primarily identify a concrete type to
            // use for deserialization. If the type we identify has a custom converter,
            // we should use that.
            // Ideally, we would just use default serialization here, now that we identified
            // the concrete type, but that doesn't work because we end up with a stack overflow.
            instance = serializer.Deserialize(jsonObject.CreateReader(), instanceType);
        }

        instance ??= instanceType != typeof(FallbackEventBody)
            ? Activator.CreateInstance(instanceType)
            : new FallbackEventBody(type); // Ugly special case.

        if (instance is null)
        {
            throw new InvalidOperationException($"Could not create instance of type {instanceType.FullName}");
        }

        serializer.Populate(jsonObject.CreateReader(), instance);
        if (instance is not IElement element)
        {
            throw new InvalidOperationException($"{instanceType.FullName} is not an IElement");
        }

        return element;
    }

    static IReadOnlyDictionary<string, Mapping> CreateElementMap()
    {
        return typeof(IElement)
            .Assembly
            .GetTypes()
            .Select(ElementInfo.Create)
            .Where(info => info != default)
            .GroupBy(info => info.JsonType)
            .ToDictionary(group => group.Key, elementInfos => new Mapping(elementInfos));
    }

    /// <summary>
    /// Information about a Slack JSON payload and how we map it to a CLR type.
    /// </summary>
    /// <param name="JsonType">The <c>type</c> value of the JSON payload.</param>
    /// <param name="ClrType">The CLR <see cref="Type"/> to deserialize the payload to.</param>
    /// <param name="RequiredProperty">If present, this property must be available to map this <see cref="JsonType"/> to the CLR <see cref="Type"/>.</param>
    /// <param name="RequiredPropertyValue">If present, the <see cref="RequiredProperty"/> value must equal the value of this property to map this <see cref="JsonType"/> to the CLR <see cref="Type"/>.</param>
    readonly record struct ElementInfo(
        string JsonType,
        Type ClrType,
        string? RequiredProperty,
        string? RequiredPropertyValue)
    {
        public static ElementInfo Create(Type type)
        {
            var attribute = type.GetCustomAttribute<ElementAttribute>();
            return attribute is null
                ? default
                : new ElementInfo(attribute.JsonType, type, attribute.Discriminator, attribute.DiscriminatorValue);
        }

        public bool HasRequiredProperty(JToken jsonObject)
        {
            if (RequiredProperty is null || jsonObject.SelectToken(RequiredProperty) is not { } token)
            {
                return false;
            }
            return RequiredPropertyValue is null || token.Value<string>() == RequiredPropertyValue;
        }
    }

    readonly record struct Mapping(IEnumerable<ElementInfo> ElementInfos)
    {
        public Type? GetClosestType(Type targetType, JObject jsonObject)
        {
            (_, var clrType, string? requiredProperty, _) = ElementInfos.FirstOrDefault(i => i.HasRequiredProperty(jsonObject));
            if (requiredProperty is not null)
            {
                return clrType;
            }

            var clrTypes = ElementInfos
                .Where(i => i.RequiredProperty is null) // We already tried matching on the required property, so filter those out now.
                .Select(i => i.ClrType)
                .ToList();
            if (clrTypes.Count is 0)
            {
                // Filtered out too much. Step it back a bit.
                clrTypes = ElementInfos
                    .Select(i => i.ClrType)
                    .ToList();
            }
            return clrTypes.Count is 1
                ? clrTypes[0]
                : clrTypes.FirstOrDefault(targetType.IsAssignableFrom);
        }
    }

    /// <summary>
    /// Concrete implementation of <see cref="Element"/> to return if we have trouble deserializing something.
    /// </summary>
    record FallbackElement(string Type) : Element(Type is { Length: > 0 } ? Type : "unknown_element");

    record FallbackLayoutBlock(string Type) : LayoutBlock(Type is { Length: > 0 }
        ? Type
        : "unknown_layout_block");

    record FallbackInteractiveElement(string Type) : InteractiveElement(Type is { Length: > 0 }
        ? Type
        : "unknown_block_element");

    record FallbackEventBody(string Type) : EventBody(Type is { Length: > 0 }
        ? Type
        : "unknown_event");
}

class NoConverter : JsonConverter
{
    public override bool CanRead => false;
    public override bool CanWrite => false;
    public override bool CanConvert(Type objectType)
    {
        throw new NotImplementedException();
    }
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
