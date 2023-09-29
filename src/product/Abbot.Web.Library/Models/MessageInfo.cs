using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Models;

/// <summary>
/// Key information about a message that can be serialized.
/// </summary>
/// <param name="MessageId">
/// Platform specific message Id that uniquely identifies this message for the purposes of API calls
/// to the platform.
/// </param>
/// <param name="Text">
/// If the signal was triggered by a message, this is the text of that message.
/// </param>
/// <param name="ThreadId">
/// Platform specific thread Id that uniquely identifies the thread in which this message was posted.
/// </param>
/// <param name="ConversationId">
/// The database Id for the <see cref="Conversation"/> in which this message was posted.
/// </param>
/// <param name="SenderId">
/// The database Id for the <see cref="Member"/> sending the message.
/// </param>
/// <param name="MessageUrl">The URL to the message.</param>
public record MessageInfo(
    string MessageId,
    string? Text,
    Uri? MessageUrl,  // Nullable for back compat.
    string? ThreadId,
    Id<Conversation>? ConversationId,
    Id<Member> SenderId)
{
    /// <summary>
    /// Creates a <see cref="MessageInfo"/> from a <see cref="MessageContext"/>.
    /// </summary>
    /// <param name="messageContext">The message context.</param>
    /// <param name="conversation">The <see cref="Conversation"/> the message is in, if any.</param>
    public static MessageInfo? FromMessageContext(MessageContext? messageContext, Conversation conversation)
    {
        return messageContext is { MessageId: not null }
            ? new MessageInfo(
                messageContext.MessageId,
                messageContext.CommandText,
                messageContext.MessageUrl,
                messageContext.ThreadId,
                conversation,
                messageContext.FromMember)
            : null;
    }

    /// <summary>
    /// Creates a <see cref="MessageInfo"/> from a <see cref="Conversation"/>.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    public static MessageInfo FromConversation(Conversation conversation)
    {
        return new MessageInfo(
            conversation.FirstMessageId,
            conversation.Title,
            conversation.GetFirstMessageUrl(),
            ThreadId: null,
            conversation,
            (Id<Member>)conversation.StartedById);
    }
}
