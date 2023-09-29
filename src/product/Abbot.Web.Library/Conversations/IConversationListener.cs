using Serious.Abbot.Entities;

namespace Serious.Abbot.Conversations;

/// <summary>
/// Responds to new messages within tracked conversations.
/// </summary>
public interface IConversationListener
{
    /// <summary>
    /// Called by <see cref="ConversationPublisher"/> when a new conversation is created.
    /// </summary>
    /// <param name="conversation">The newly-created <see cref="Conversation"/></param>
    /// <param name="message">The <see cref="ConversationMessage"/> that triggered the message.</param>
    Task OnNewConversationAsync(Conversation conversation, ConversationMessage message) => Task.CompletedTask;

    /// <summary>
    /// Called by <see cref="ConversationPublisher"/> when a new message is received on an existing conversation.
    /// </summary>
    /// <remarks>
    /// This is not called for the first message in a conversation. Use
    /// <see cref="OnNewConversationAsync(Conversation, ConversationMessage)"/> for that.
    /// </remarks>
    /// <param name="conversation">The <see cref="Conversation"/> on which the new message was received.</param>
    /// <param name="message">The <see cref="ConversationMessage"/> that was received.</param>
    Task OnNewMessageAsync(Conversation conversation, ConversationMessage message) => Task.CompletedTask;

    /// <summary>
    /// Called by <see cref="ConversationPublisher"/> when <see cref="Conversation.State"/> has changed.
    /// </summary>
    /// <param name="stateChange">The <see cref="StateChangedEvent"/>.</param>
    Task OnStateChangedAsync(StateChangedEvent stateChange) => Task.CompletedTask;
}
