using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;

namespace Serious.TestHelpers;

public class FakeConversationPublisher : IConversationPublisher
{
    public List<PublishedMessage> PublishedMessages { get; } = new();
    public List<StateChangedEvent> PublishedStateChanges { get; } = new();

    public Task PublishNewConversationAsync(Conversation conversation, ConversationMessage message, MessagePostedEvent messagePostedEvent)
    {
        PublishedMessages.Add(new(typeof(NewConversation), conversation, message, messagePostedEvent));
        return Task.CompletedTask;
    }

    public Task PublishNewMessageInConversationAsync(Conversation conversation, ConversationMessage message, MessagePostedEvent messagePostedEvent)
    {
        PublishedMessages.Add(new(typeof(NewMessageInConversation), conversation, message, messagePostedEvent));
        return Task.CompletedTask;
    }

    public Task PublishConversationStateChangedAsync(StateChangedEvent stateChangedEvent)
    {
        PublishedStateChanges.Add(stateChangedEvent);
        return Task.CompletedTask;
    }

    public record PublishedMessage(
        Type MessageType,
        Conversation Conversation,
        ConversationMessage ConversationMessage,
        MessagePostedEvent MessagePostedEvent);
}
