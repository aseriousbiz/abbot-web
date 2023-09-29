using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Serious.Abbot.Serialization;

public static class JTokenStripper
{
    public static IDictionary<string, object?> StripJTokens(this IDictionary<string, object?> values)
    {
        foreach (var (k, v) in values)
        {
            values[k] = StripJTokens(v);
        }
        return values;
    }

    public static object? StripJTokens(object? value) => value switch
    {
        not JToken => value,
        JArray ja => ja.Select(StripJTokens).ToArray(),
        JObject jo => jo.Properties().ToDictionary(
            kvp => kvp.Name,
            kvp => StripJTokens(kvp.Value)
        ),
        JToken jt => jt.Type switch
        {
            JTokenType.Integer => jt.ToObject<long>(),
            JTokenType.Float => jt.ToObject<double>(),
            JTokenType.String => jt.ToObject<string>(),
            JTokenType.Boolean => jt.ToObject<bool>(),
            JTokenType.Null => null,
            JTokenType.Undefined => null,
            JTokenType.Date => jt.ToObject<DateTime>(),
            JTokenType.Bytes => jt.ToObject<byte[]>(),
            JTokenType.Guid => jt.ToObject<Guid>(),
            JTokenType.Uri => jt.ToObject<Uri>(),
            JTokenType.TimeSpan => jt.ToObject<TimeSpan>(),
            _ => throw new NotImplementedException($"Unexpected JTokenType: {jt.Type}"),
        },
    };
}
