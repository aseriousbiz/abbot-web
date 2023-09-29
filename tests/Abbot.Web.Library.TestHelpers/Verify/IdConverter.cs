using System.Reflection;
using Argon;
using Serious;

namespace Abbot.Common.TestHelpers.Verify;

public class IdConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        Expect.True(value.GetType().GetGenericTypeDefinition() == typeof(Id<>));
        var prop = value.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        var val = (int)prop.Require().GetValue(value).Require();
        writer.WriteValue(val);
    }

    public override object? ReadJson(JsonReader reader, Type type, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Integer)
        {
            var val = reader.ReadAsInt32();
            return Activator.CreateInstance(type, val);
        }
        throw new JsonException("Cannot deserialize Id from " + reader.TokenType);
    }

    public override bool CanConvert(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Id<>);
    }
}
