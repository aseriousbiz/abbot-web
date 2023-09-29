namespace Serious.Abbot.Messages;

/// <summary>
/// A request to update data for a skill (Bot.Brain).
/// </summary>
public class SkillDataUpdateRequest
{
    /// <summary>
    /// The new value of the data item.
    /// </summary>
    public string Value { get; init; } = null!;

    /// <summary>
    /// The scope of the skill, used during the creation step
    /// </summary>
    public SkillDataScope Scope { get; init; }

    /// <summary>
    /// Id that tracks the state and context of chat messages.
    /// If the skill scope is set to user, this is the UserId
    /// If the skill scope is set to Conversation, this is the ConversationId
    /// If the skill scope is set to Room, this is the RoomId
    /// </summary>
    public string? ContextId { get; init; }
}
