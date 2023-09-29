using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Signals;

namespace Serious.Abbot.Conversations;

public class ConversationWelcomeListener : IConversationListener
{
    readonly ISystemSignaler _systemSignaler;
    readonly PlaybookDispatcher _playbookDispatcher;

    public ConversationWelcomeListener(
        ISystemSignaler systemSignaler,
        PlaybookDispatcher playbookDispatcher)
    {
        _systemSignaler = systemSignaler;
        _playbookDispatcher = playbookDispatcher;
    }

    public async Task OnNewConversationAsync(Conversation conversation, ConversationMessage message)
    {
        // We should only welcome the conversation if it's not Hidden and a live conversation.
        // We don't want to trigger system signals or auto responders for imported messages.
        if (message.IsLive && conversation.State is not ConversationState.Hidden)
        {
            _systemSignaler.EnqueueSystemSignal(
                SystemSignal.ConversationStartedSignal,
                arguments: message.Text,
                message.Organization,
                message.Room.ToPlatformRoom(),
                message.From,
                MessageInfo.FromMessageContext(message.MessageContext, conversation));

            var outputs = new OutputsBuilder()
                .SetMessage(conversation.Room, message)
                .SetConversation(conversation)
                .Outputs;
            await _playbookDispatcher.DispatchAsync(
                ConversationStartedTrigger.Id,
                outputs,
                conversation.Organization,
                PlaybookRunRelatedEntities.From(conversation));

            // Welcome the user to the conversation, if configured
            var settings = RoomSettings.Merge(RoomSettings.Default, message.Organization.DefaultRoomSettings);

            if (settings is { WelcomeNewConversations: true, ConversationWelcomeMessage: { Length: > 0 } welcome })
            {
                await message.MessageContext.SendActivityAsync(welcome, true);
            }
        }
    }
}
