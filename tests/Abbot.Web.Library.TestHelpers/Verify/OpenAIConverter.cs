using Argon;
using OpenAI_API.Chat;

namespace Abbot.Common.TestHelpers.Verify;

public class OpenAIConverter : Argon.JsonConverter
{
    public override bool CanRead => false;
    public override object? ReadJson(JsonReader reader, Type type, object? existingValue, JsonSerializer serializer) =>
        throw new NotImplementedException();

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is ChatMessage chatMessage)
        {
            writer.WriteValue($"---\n[{chatMessage.Role}]\n{chatMessage.Content}\n---");
            return;
        }

        throw new NotSupportedException();
    }

    public override bool CanConvert(Type type)
    {
        return type == typeof(ChatMessage);
    }
}
