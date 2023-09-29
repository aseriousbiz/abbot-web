using System;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Serious.Cryptography;

public class SecretStringConverter : JsonConverter<SecretString?>
{
    public override void WriteJson(JsonWriter writer, SecretString? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue(value.ProtectedValue);
        }
    }

    public override SecretString? ReadJson(JsonReader reader, Type objectType, SecretString? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonToken.String)
        {
            throw new JsonSerializationException(
                $"Cannot deserialize a {reader.TokenType} into a {nameof(SecretString)}");
        }
        return (string)reader.Value;
    }
}
