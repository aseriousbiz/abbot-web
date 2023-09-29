using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Xunit;

public class MessageChangedHandlerTests
{
    public class TheOnPlatformEventAsyncMethod
    {
        [Fact]
        public async Task WithMessageChangedEventForDeletedMessageArchivesConversation()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            Assert.NotEqual(ConversationState.Archived, conversation.State);
            var from = env.TestData.ForeignMember;
            var payload = new MessageChangedEvent
            {
                SubType = "message_changed",
                Message = new SlackMessage
                {
                    SubType = "tombstone",
                    Timestamp = conversation.FirstMessageId
                }
            };
            var changeEvent = env.CreateFakePlatformRoomEvent(payload, room, from);
            var handler = env.Activate<MessageChangedHandler>();

            await handler.OnPlatformEventAsync(changeEvent);

            Assert.Equal(ConversationState.Archived, conversation.State);
        }
    }
}
