using System.Collections.Generic;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Conversations;

/// <summary>
/// Manages conversation state whenever new messages are posted.
/// </summary>
public interface IConversationTracker
{
    /// <summary>
    /// Try to create a new conversation from this message, but only if the thread meets our criteria for a
    /// conversation.
    /// </summary>
    /// <param name="message">A <see cref="ConversationMessage"/> representing the message to process.</param>
    /// <param name="utcNow">The current date and time.</param>
    /// <param name="conversationMatchAIResult">If this conversation was matched by AI, this contains the result.</param>
    /// <returns>A <see cref="Conversation" />, the result of processing this message is to create a new conversation. Otherwise, <c>null</c>.</returns>
    Task<Conversation?> TryCreateNewConversationAsync(
        ConversationMessage message,
        DateTime utcNow,
        ConversationMatchAIResult? conversationMatchAIResult = null);

    /// <summary>
    /// Updates conversation state based on the provided message.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> to update.</param>
    /// <param name="message">A <see cref="ConversationMessage"/> representing the message to process.</param>
    Task UpdateConversationAsync(Conversation conversation, ConversationMessage message);

    /// <summary>
    /// Imports a conversation from a set of messages.
    /// </summary>
    /// <param name="messages">The messages to import.</param>
    /// <param name="actor">The <see cref="Member"/> performing the import. May be the 'abbot' user if the import is being performed by Staff.</param>
    /// <param name="utcTimestamp">The UTC time at which the import was performed.</param>
    /// <returns>The imported <see cref="Conversation"/>, if it could be created.</returns>
    Task<Conversation?> CreateConversationAsync(
        IReadOnlyList<ConversationMessage> messages,
        Member actor, DateTime utcTimestamp);
}
