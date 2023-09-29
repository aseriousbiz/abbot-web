namespace Serious.Abbot.Scripting;

/// <summary>
/// Information about a conversation.
/// </summary>
/// <param name="Id">The database id of the conversation.</param>
/// <param name="FirstMessageId">The platform-specific message Id of the first message in the conversation.</param>
/// <param name="Title">The title of the conversation.</param>
public record ChatConversationInfo(
    string Id,
    string FirstMessageId,
    string Title);
