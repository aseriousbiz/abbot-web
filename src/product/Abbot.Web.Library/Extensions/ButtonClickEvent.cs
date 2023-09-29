using Newtonsoft.Json.Linq;

namespace Serious.Abbot.Extensions;

public class ButtonClickEvent
{
    public static ButtonClickEvent? FromJObject(object value)
    {
        return value is not JObject json
            ? null
            : json.ToObject<ButtonClickEvent>();
    }

    public int SkillId { get; set; }

    /// <summary>
    /// Id that tracks the state and context of chat messages.
    /// If the skill scope is set to user, this is the UserId
    /// If the skill scope is set to Conversation, this is the ConversationId
    /// If the skill scope is set to Room, this is the RoomId
    /// </summary>
    public string? ContextId { get; set; }

    public string Args { get; set; } = null!;
}
