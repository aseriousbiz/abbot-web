using Argon;
using Serious.Abbot.Serialization;
using JToken = Newtonsoft.Json.Linq.JToken;

namespace Abbot.Common.TestHelpers.Verify;

public class NewtonsoftJTokenConverter : Argon.JsonConverter<JToken>
{
    public override JToken? ReadJson(JsonReader reader, Type type, JToken? existingValue, bool hasExisting, JsonSerializer serializer) =>
        throw new NotImplementedException();

    public override void WriteJson(JsonWriter writer, JToken? value, JsonSerializer serializer)
    {
        // TODO: Respect serializer.Formatting == Formatting.Indented?
        var raw = AbbotJsonFormat.NewtonsoftJson.Serialize(value);
        writer.WriteRawValue(raw);
    }
}
