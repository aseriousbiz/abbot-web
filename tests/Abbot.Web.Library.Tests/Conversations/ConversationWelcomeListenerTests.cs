using Abbot.Common.TestHelpers;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Services;
using Serious.Abbot.Signals;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.TestHelpers;

public class ConversationWelcomeListenerTests
{
    public class TheOnNewConversationAsyncMethod
    {
        [Fact]
        public async Task RaisesSystemSignalIfMessageIsLive()
        {
            var env = TestEnvironment.Create();
            var listener = env.Activate<ConversationWelcomeListener>();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room, firstMessageId: "8675309.121212");

            var messageInfo = new MessageInfo(
                "23493239.3242",
                "The command text",
                new Uri($"https://{env.TestData.Organization.Domain}/archives/{room.PlatformRoomId}/p234932393242"),
                null,
                convo,
                env.TestData.Member);
            var context = env.CreateFakeMessageContext(
                messageId: messageInfo.MessageId,
                threadId: messageInfo.ThreadId,
                commandText: "The command text",
                conversation: convo,
                from: env.TestData.Member,
                room: room);
            var message = new ConversationMessage(
                "The command text",
                env.TestData.Organization,
                env.TestData.Member,
                room,
                DateTime.UtcNow,
                "1234",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                context);

            await listener.OnNewConversationAsync(convo, message);

            env.SignalHandler.AssertRaised(
                SystemSignal.ConversationStartedSignal.Name,
                "The command text",
                room.PlatformRoomId,
                env.TestData.Member,
                messageInfo);
        }

        [Fact]
        public async Task IgnoresHiddenConversations()
        {
            var env = TestEnvironment.Create();
            var listener = env.Activate<ConversationWelcomeListener>();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);

            var context = FakeMessageContext.Create();
            var message = new ConversationMessage(
                "The message",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                DateTime.UtcNow,
                "1234",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                context);

            await listener.OnNewConversationAsync(convo, message);

            env.SignalHandler.AssertNotRaised(SystemSignal.ConversationStartedSignal.Name);
        }

        [Theory]
        [InlineData(false, null, null)]
        [InlineData(true, null, RoomSettings.DefaultConversationWelcomeMessage)]
        [InlineData(false, "Yo dude", null)]
        [InlineData(true, "Yo dude", "Yo dude")]
        public async Task SendsWelcomeMessageIfMessageIsLiveAndWelcomeEnabled(
            bool welcomeMessageEnabled,
            string? welcomeMessage,
            string? expectedMessage)
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.DefaultRoomSettings = new()
            {
                WelcomeNewConversations = welcomeMessageEnabled,
                ConversationWelcomeMessage = welcomeMessage,
            };

            var listener = env.Activate<ConversationWelcomeListener>();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var message = env.CreateFakeMessageContext(
                room: room,
                from: env.TestData.ForeignMember,
                messageId: "1234.5678",
                threadId: null);

            var convoMessage = ConversationMessage.CreateFromLiveMessage(message, Array.Empty<SensitiveValue>());
            await listener.OnNewConversationAsync(convo, convoMessage);

            Assert.Equal(expectedMessage, message.SentMessages.SingleOrDefault());
        }
    }
}
