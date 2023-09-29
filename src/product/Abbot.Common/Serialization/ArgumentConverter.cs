using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Serialization;

/// <summary>
/// Converts incoming arguments in <see cref="SkillMessage" /> to their proper argument type.
/// </summary>
public class ArgumentConverter : JsonConverter
{
    // Use the default behavior for writing
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
        throw new NotImplementedException();

    public override object ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);

        var item = PropertyExists(jsonObject, nameof(MentionArgument.Mentioned))
            ? new MentionArgument()
            : PropertyExists(jsonObject, nameof(RoomArgument.Room))
                ? new RoomArgument()
                : jsonObject["value"] is JValue { Type: JTokenType.Integer } value
                    ? new Int32Argument()
                    : new Argument();

        serializer.Populate(jsonObject.CreateReader(), item);

        return item;
    }

    static bool PropertyExists(JObject jsonObject, string name) =>
        jsonObject[name] is not null;

    public override bool CanConvert(Type objectType) =>
        typeof(Argument) == objectType;
}
