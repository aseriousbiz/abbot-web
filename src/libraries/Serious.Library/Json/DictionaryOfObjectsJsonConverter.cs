using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Serious.Json;

/// <summary>
/// Unwraps <see cref="JsonElement"/>s from a deserialized dictionary.
/// </summary>
public class DictionaryOfObjectsJsonConverter : JsonConverter<IDictionary<string, object?>>
{
    public override IDictionary<string, object?>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = JsonSerializer.Deserialize<IDictionary<string, object?>>(ref reader, options);
        if (result is not null)
        {
            foreach (var (key, value) in result)
            {
                var o = value is JsonElement je ? je.ToObject() : value;
                result[key] = o;
            }
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, IDictionary<string, object?> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
