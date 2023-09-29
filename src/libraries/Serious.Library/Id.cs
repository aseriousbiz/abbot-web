using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;
using STJ = System.Text.Json;

namespace Serious;

/// <summary>
/// A strongly typed struct that can be used as a "guarded" primary key for an entity.
/// </summary>
/// <param name="Value">The underlying database value.</param>
/// <typeparam name="T">The type this is an Id for.</typeparam>
[JsonConverter(typeof(IdJsonConverter.NewtonsoftJson))]
[System.Text.Json.Serialization.JsonConverter(typeof(IdJsonConverter.SystemTextJson))]
public readonly record struct Id<T>(int Value) : IParsable<Id<T>> where T : class
{
    public static explicit operator Id<T>(int id) => new(id);
    public static explicit operator Id<T>?(int? id) => id == null ? null : new(id.Value);
    public static implicit operator int(Id<T> id) => id.Value;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc cref="Parse(string?, IFormatProvider?)"/>
#pragma warning disable CA1000
    public static Id<T> Parse([NotNull] string? s) => Parse(s, null);

    public static Id<T> Parse([NotNull] string? s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return new(int.Parse(s, provider));
    }

    /// <inheritdoc cref="TryParse(string?, IFormatProvider?, out Id{T})"/>
    public static bool TryParse([NotNullWhen(true)] string? s, out Id<T> result) => TryParse(s, null, out result);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Id<T> result)
    {
        if (s is null || !int.TryParse(s, provider, out var value))
        {
            result = default;
            return false;
        }

        result = new Id<T>(value);
        return true;
    }
#pragma warning restore CA1000

}

/// <summary>
/// Helper methods for <see cref="Id{T}"/>.
/// </summary>
public static class Id
{
    public static Id<T>? Null<T>() where T : class => null;
}

static class IdJsonConverter
{
    internal class SystemTextJson : STJ.Serialization.JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Id<>);
        }

        public override STJ.Serialization.JsonConverter? CreateConverter(Type typeToConvert, STJ.JsonSerializerOptions options)
        {
            if (!typeToConvert.IsGenericType || typeToConvert.GetGenericTypeDefinition() != typeof(Id<>))
            {
                return null;
            }

            var converter = typeof(SystemTextJson<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]);
            return Activator.CreateInstance(converter).Require<STJ.Serialization.JsonConverter>();
        }
    }

    internal class SystemTextJson<TEntity> : STJ.Serialization.JsonConverter<Id<TEntity>> where TEntity : class
    {
        public override Id<TEntity> Read(ref STJ.Utf8JsonReader reader, Type typeToConvert, STJ.JsonSerializerOptions options)
        {
            if (reader.TokenType == STJ.JsonTokenType.Number)
            {
                return new Id<TEntity>(reader.GetInt32());
            }

            if (reader.TokenType == STJ.JsonTokenType.StartObject)
            {
                int? value = null;
                while (reader.Read())
                {
                    if (reader.TokenType == STJ.JsonTokenType.EndObject)
                    {
                        if (value is null)
                        {
                            throw new STJ.JsonException("Expected a 'Value' property.");
                        }
                        return new Id<TEntity>(value.Value);
                    }

                    if (reader.TokenType != STJ.JsonTokenType.PropertyName)
                    {
                        throw new STJ.JsonException("Expected a 'Value' property.");
                    }

                    if (string.Equals("value", reader.GetString(), StringComparison.OrdinalIgnoreCase))
                    {
                        reader.Read();
                        if (reader.TokenType != STJ.JsonTokenType.Number)
                        {
                            throw new STJ.JsonException("Expected 'Value' property to be a number.");
                        }

                        value = reader.GetInt32();
                    }

                    // Ignore other properties.
                }
                throw new STJ.JsonException("Expected a 'Value' property");
            }

            throw new STJ.JsonException("Expected a number or an object with a 'Value' property");
        }

        public override void Write(STJ.Utf8JsonWriter writer, Id<TEntity> value, STJ.JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Value);
    }

    internal class NewtonsoftJson : JsonConverter
    {
        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            var idValue = (int)(value.GetType().GetProperty("Value")?.GetValue(value) ?? 0);
            serializer.Serialize(writer, idValue);
        }

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            var intValue = serializer.Deserialize<int?>(reader);

            if (Nullable.GetUnderlyingType(objectType) is { } underlyingType)
            {
                return intValue is null
                    ? null :
                    // Create nullable Id of the underlying generic type using the intValue and Activator.
                    Activator.CreateInstance(underlyingType, intValue.Value);
            }
            return Activator.CreateInstance(objectType, intValue.GetValueOrDefault());
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.GetGenericTypeDefinition() == typeof(Id<>);
        }
    }
}
