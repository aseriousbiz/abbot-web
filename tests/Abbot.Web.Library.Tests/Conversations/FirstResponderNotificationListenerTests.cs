using Abbot.Common.TestHelpers;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.TestHelpers;

public class FirstResponderNotificationListenerTests
{
    public class TheOnNewConversationAsyncMethod
    {
        [Fact]
        public async Task NotifiesRespondersThatHaveNotPreviouslyBeenNotified()
        {
            var env = TestEnvironment.Create();
            var fr1 = await env.CreateMemberInAgentRoleAsync();
            var fr2 = await env.CreateMemberInAgentRoleAsync();
            var fr3 = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync();
            await env.Rooms.AssignMemberAsync(room, fr1, RoomRole.FirstResponder, env.TestData.Member);
            await env.Rooms.AssignMemberAsync(room, fr2, RoomRole.FirstResponder, env.TestData.Member);
            await env.Rooms.AssignMemberAsync(room, fr3, RoomRole.FirstResponder, env.TestData.Member);
            var convo = await env.CreateConversationAsync(room);

            // Mark fr2 as notified
            await env.Settings.SetAsync(
                SettingsScope.Member(fr2),
                FirstResponderNotificationListener.SettingKey,
                "true",
                env.TestData.Abbot.User);

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

            var listener = env.Activate<FirstResponderNotificationListener>();

            await listener.OnNewConversationAsync(convo, message);

            // All FRs marked as notified
            Assert.Equal("true", (await env.Settings.GetAsync(SettingsScope.Member(fr1), FirstResponderNotificationListener.SettingKey))?.Value);
            Assert.Equal("true", (await env.Settings.GetAsync(SettingsScope.Member(fr2), FirstResponderNotificationListener.SettingKey))?.Value);
            Assert.Equal("true", (await env.Settings.GetAsync(SettingsScope.Member(fr3), FirstResponderNotificationListener.SettingKey))?.Value);

            // Messages sent to FR1 and FR2
            Assert.Collection(env.SlackApi.PostedMessages,
                m => {
                    Assert.Equal(fr1.User.PlatformUserId, m.Channel);
                    Assert.StartsWith("I’m tracking conversations", m.Text);
                },
                m => {
                    Assert.Equal(fr3.User.PlatformUserId, m.Channel);
                    Assert.StartsWith("I’m tracking conversations", m.Text);
                });
        }

        [Fact]
        public async Task IgnoresImportedConversation()
        {
            var env = TestEnvironment.Create();
            var fr1 = await env.CreateMemberAsync();
            var fr2 = await env.CreateMemberAsync();
            var fr3 = await env.CreateMemberAsync();
            var room = await env.CreateRoomAsync();
            await env.Rooms.AssignMemberAsync(room, fr1, RoomRole.FirstResponder, env.TestData.Member);
            await env.Rooms.AssignMemberAsync(room, fr2, RoomRole.FirstResponder, env.TestData.Member);
            await env.Rooms.AssignMemberAsync(room, fr3, RoomRole.FirstResponder, env.TestData.Member);
            var convo = await env.CreateConversationAsync(room);

            // Mark fr2 as notified
            await env.Settings.SetAsync(
                SettingsScope.Member(fr2),
                FirstResponderNotificationListener.SettingKey,
                "true",
                env.TestData.Abbot.User);

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
                null);

            var listener = env.Activate<FirstResponderNotificationListener>();

            await listener.OnNewConversationAsync(convo, message);

            // No other FRs marked as notified
            Assert.Null((await env.Settings.GetAsync(SettingsScope.Member(fr1), FirstResponderNotificationListener.SettingKey))?.Value);
            Assert.Equal("true", (await env.Settings.GetAsync(SettingsScope.Member(fr2), FirstResponderNotificationListener.SettingKey))?.Value);
            Assert.Null((await env.Settings.GetAsync(SettingsScope.Member(fr3), FirstResponderNotificationListener.SettingKey))?.Value);

            // No messages sent
            Assert.Empty(env.SlackApi.PostedMessages);
        }

        [Fact]
        public async Task IgnoresHiddenConversation()
        {
            var env = TestEnvironment.Create();
            var fr1 = await env.CreateMemberInAgentRoleAsync();
            var fr2 = await env.CreateMemberInAgentRoleAsync();
            var fr3 = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync();
            await env.Rooms.AssignMemberAsync(room, fr1, RoomRole.FirstResponder, env.TestData.Member);
            await env.Rooms.AssignMemberAsync(room, fr2, RoomRole.FirstResponder, env.TestData.Member);
            await env.Rooms.AssignMemberAsync(room, fr3, RoomRole.FirstResponder, env.TestData.Member);
            var convo = await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);

            // Mark fr2 as notified
            await env.Settings.SetAsync(
                SettingsScope.Member(fr2),
                FirstResponderNotificationListener.SettingKey,
                "true",
                env.TestData.Abbot.User);

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

            var listener = env.Activate<FirstResponderNotificationListener>();

            await listener.OnNewConversationAsync(convo, message);


            // No other FRs marked as notified
            Assert.Null((await env.Settings.GetAsync(SettingsScope.Member(fr1), FirstResponderNotificationListener.SettingKey))?.Value);
            Assert.Equal("true", (await env.Settings.GetAsync(SettingsScope.Member(fr2), FirstResponderNotificationListener.SettingKey))?.Value);
            Assert.Null((await env.Settings.GetAsync(SettingsScope.Member(fr3), FirstResponderNotificationListener.SettingKey))?.Value);

            // No messages sent
            Assert.Empty(env.SlackApi.PostedMessages);
        }
    }
}
