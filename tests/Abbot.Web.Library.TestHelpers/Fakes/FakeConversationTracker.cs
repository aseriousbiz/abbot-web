using MassTransit;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Live;
using Serious.Abbot.Repositories;

namespace Serious.TestHelpers;

public class FakeConversationTracker : IConversationTracker
{
    readonly IConversationRepository _conversationRepository;
    readonly ConversationTracker _conversationTracker;

    public FakeConversationTracker(
        IConversationRepository conversationRepository,
        IConversationPublisher conversationPublisher)
    {
        // This is cheesy, but I don't like reproducing logic of ConversationTracker in this class.
        // Until I have time to improve this, this will have to do.
        _conversationTracker = new ConversationTracker(
            conversationRepository,
            conversationPublisher);

        _conversationRepository = conversationRepository;
    }

    public IList<(Conversation Conversation, ConversationMessage Message)> UpdatesReceived { get; } = new List<(Conversation Conversation, ConversationMessage Message)>();

    public IList<ConversationMessage> ConversationMessagesReceived { get; } = new List<ConversationMessage>();

    public IDictionary<string, Conversation> ThreadIdToConversationMappings { get; } = new Dictionary<string, Conversation>();

    public Task<Conversation?> TryCreateNewConversationAsync(
        ConversationMessage message,
        DateTime utcNow,
        ConversationMatchAIResult? conversationMatchAIResult = null) =>
        TryCreateNewConversationAsync(
            message,
            utcNow,
            conversationMatchAIResult,
            force: false);

    async Task<Conversation?> TryCreateNewConversationAsync(
        ConversationMessage message,
        DateTime utcNow,
        ConversationMatchAIResult? conversationMatchAIResult,
        bool force = false)
    {
        ConversationMessagesReceived.Add(message);
        var convo = await _conversationTracker.TryCreateNewConversationAsync(
            message,
            utcNow,
            conversationMatchAIResult,
            force);
        if (convo is not null)
        {
            ThreadIdToConversationMappings.Add(message.ThreadId ?? message.MessageId, convo);
        }
        return convo;
    }

    public Task UpdateConversationAsync(Conversation conversation, ConversationMessage message)
    {
        ConversationMessagesReceived.Add(message);
        UpdatesReceived.Add((conversation, message));

        _conversationRepository.UpdateForNewMessageAsync(conversation,
            new MessagePostedEvent { MessageId = message.MessageId },
            message,
            ConversationTracker.IsSupportee(message.From, message.Room));

        return Task.CompletedTask;
    }

    public async Task<Conversation?> CreateConversationAsync(
        IReadOnlyList<ConversationMessage> messages,
        Member actor,
        DateTime utcTimestamp)
    {
        var convo = await TryCreateNewConversationAsync(
            messages.First(),
            utcTimestamp,
            null,
            force: true).Require();
        if (convo.State is not ConversationState.Hidden)
        {
            convo.State = ConversationState.Closed;
        }
        return convo;
    }
}
