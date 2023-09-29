using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Serious.Json;

public static class JsonHelpers
{
    public static Dictionary<string, object?> DeserializeJsonToDictionary(string jsonString)
    {
        var dictionary = new Dictionary<string, object?>();

        var jsonDocument = JsonDocument.Parse(jsonString);
        var root = jsonDocument.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                var value = property.Value.ToObject();
                dictionary.Add(property.Name, value);
            }
        }

        return dictionary;
    }

    public static object? ToObject(this JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            JsonValueKind.Object => DeserializeJsonToDictionary(jsonElement.GetRawText()),
            JsonValueKind.Array => jsonElement.EnumerateArray().Select(ToObject).ToArray(),
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number =>
                // Check if the number is an integer or a floating-point value
                jsonElement.TryGetInt64(out var longValue)
                    ? (object?)longValue
                    : jsonElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => null
        };
    }
}
