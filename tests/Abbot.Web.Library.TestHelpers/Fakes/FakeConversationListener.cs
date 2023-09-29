using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeConversationListener : IConversationListener
{
    public IList<(Conversation Conversation, ConversationMessage)> NewConversationsObserved { get; } = new List<(Conversation, ConversationMessage)>();
    public IList<(Conversation Conversation, ConversationMessage)> NewMessagesObserved { get; } = new List<(Conversation, ConversationMessage)>();
    public List<StateChangedEvent> StateChangesObserved { get; } = new();

    public Task OnNewConversationAsync(Conversation conversation, ConversationMessage message)
    {
        NewConversationsObserved.Add((conversation, message));
        return Task.CompletedTask;
    }

    public Task OnNewMessageAsync(Conversation conversation, ConversationMessage message)
    {
        NewMessagesObserved.Add((conversation, message));
        return Task.CompletedTask;
    }

    public Task OnStateChangedAsync(StateChangedEvent stateChange)
    {
        StateChangesObserved.Add(stateChange);
        return Task.CompletedTask;
    }
}
