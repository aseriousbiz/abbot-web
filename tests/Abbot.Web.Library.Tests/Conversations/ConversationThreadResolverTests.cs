using Abbot.Common.TestHelpers;
using Serious.Abbot.Conversations;
using Serious.Slack.InteractiveMessages;

public class ConversationThreadResolverTests
{
    public class TheResolveConversationMessagesAsyncMethod
    {
        [Theory]
        [InlineData(false, true, true, false, "Cannot import message, organization has no API token.")]
        [InlineData(true, false, false, true, "Cannot import message, bot is not known to be a member.")]
        [InlineData(true, true, true, false, "Failed to import messages from Slack: Error: not_found\n")]
        [InlineData(true, true, false, true, "Failed to import messages from Slack: No messages returned")]
        public async Task ThrowsIfPreconditionNotMet(bool apiTokenPresent, bool botIsMember, bool slackResponseFails, bool emptyResponse, string message)
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.ApiToken = apiTokenPresent
                ? env.Secret("the-token")
                : null;
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync(botIsMember: botIsMember);

            if (emptyResponse)
            {
                env.SlackApi.Conversations.AddConversationRepliesResponse("the-token",
                    room.PlatformRoomId,
                    "1234",
                    Array.Empty<SlackMessage>());
            }
            else if (!slackResponseFails)
            {
                env.SlackApi.Conversations.AddConversationRepliesResponse("the-token",
                    room.PlatformRoomId,
                    "1234",
                    new SlackMessage[]
                    {
                        new() { Text = "Whoop" },
                    });
            }

            var resolver = env.Activate<ConversationThreadResolver>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveConversationMessagesAsync(room, "1234"));
            Assert.Equal(message, ex.Message);
        }

        [Fact]
        public async Task IgnoresConversationIfIfMemberCannotBeResolved()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            env.TestData.Organization.ApiToken = env.Secret("the-token");
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();
            env.SlackApi.Conversations.AddConversationRepliesResponse("the-token",
                room.PlatformRoomId,
                "1234",
                new SlackMessage[]
                {
                    new() { Text = "Question?", User = env.TestData.ForeignUser.PlatformUserId, Timestamp = TimestampFromTime(env.Clock.UtcNow, "1111") },
                    new() { Text = "Follow-up.", User = env.TestData.User.PlatformUserId, Timestamp = TimestampFromTime(env.Clock.UtcNow, "2222")  },
                    new() { Text = "Exclamation!", User = NonExistent.PlatformUserId, Timestamp = TimestampFromTime(env.Clock.UtcNow, "3333") },
                });
            var resolver = env.Activate<ConversationThreadResolver>();

            var result = await resolver.ResolveConversationMessagesAsync(room, "1234");
            Assert.Equal(2, result.Count);
            Assert.Equal("Question?", result[0].Text);
            Assert.Equal("Follow-up.", result[1].Text);
        }

        [Fact]
        public async Task ReturnsConversationMessagesIfSuccessfulAndIgnoresBotMessages()
        {
            var env = TestEnvironment.Create();

            // Convert to unix epoch seconds and back to ensure we line up with Slack timestamp computations which are always in seconds.
            // Essentially, we `0` out the milliseconds part.
            var now = env.Clock.UtcNow;
            var unixSeconds = (int)(now - DateTime.UnixEpoch).TotalSeconds;
            env.Clock.TravelTo(DateTime.UnixEpoch.AddSeconds(unixSeconds));

            env.TestData.Organization.ApiToken = env.Secret("the-token");
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();

            var rootMessageId = TimestampFromTime(env.Clock.UtcNow, "1111");
            env.SlackApi.Conversations.AddConversationRepliesResponse("the-token",
                room.PlatformRoomId,
                "1234",
                new SlackMessage[]
                {
                    new()
                    {
                        Text = "Question?",
                        User = env.TestData.ForeignUser.PlatformUserId,
                        Timestamp = rootMessageId,
                    },
                    new()
                    {
                        Text = "Robot Interruption.",
                        User = env.TestData.Abbot.User.PlatformUserId,
                        SubType = "bot_message",
                        Timestamp = TimestampFromTime(env.Clock.UtcNow, "2222"),
                        ThreadTimestamp = rootMessageId,
                    },
                    new() // TODO: new BotMessage
                    {
                        Text = "Bot Message.",
                        User = null,
                        // TODO: BotId = env.TestData.Organization.PlatformBotId,
                        SubType = "bot_message",
                        Timestamp = TimestampFromTime(env.Clock.UtcNow, "2233"),
                        ThreadTimestamp = rootMessageId,
                        // TODO: BotProfile = new BotProfile()
                    },
                    new()
                    {
                        Text = "Answer.",
                        User = env.TestData.User.PlatformUserId,
                        Timestamp = TimestampFromTime(env.Clock.UtcNow, "3333"),
                        ThreadTimestamp = rootMessageId,
                    },
                });

            var resolver = env.Activate<ConversationThreadResolver>();

            var results = await resolver.ResolveConversationMessagesAsync(room, "1234");

            Assert.Collection(results,
                msg1 => {
                    Assert.Equal("Question?", msg1.Text);
                    Assert.Equal(TimestampFromTime(env.Clock.UtcNow, "1111"), msg1.MessageId);
                    Assert.Equal(env.Clock.UtcNow, msg1.UtcTimestamp);
                    Assert.Same(room, msg1.Room);
                    Assert.Same(env.TestData.Organization, msg1.Organization);
                    Assert.Same(env.TestData.ForeignMember, msg1.From);
                    Assert.Null(msg1.ThreadId);
                    Assert.False(msg1.IsLive);
                    Assert.Null(msg1.MessageContext);
                },
                msg => {
                    Assert.Equal("Robot Interruption.", msg.Text);
                    Assert.Equal(TimestampFromTime(env.Clock.UtcNow, "2222"), msg.MessageId);
                    Assert.Equal(env.Clock.UtcNow, msg.UtcTimestamp);
                    Assert.Same(room, msg.Room);
                    Assert.Same(env.TestData.Organization, msg.Organization);
                    Assert.Same(env.TestData.Abbot, msg.From);
                    Assert.Equal(TimestampFromTime(env.Clock.UtcNow, "1111"), msg.ThreadId);
                    Assert.False(msg.IsLive);
                    Assert.Null(msg.MessageContext);
                },
                msg2 => {
                    Assert.Equal("Answer.", msg2.Text);
                    Assert.Equal(TimestampFromTime(env.Clock.UtcNow, "3333"), msg2.MessageId);
                    Assert.Equal(env.Clock.UtcNow, msg2.UtcTimestamp);
                    Assert.Same(room, msg2.Room);
                    Assert.Same(env.TestData.Organization, msg2.Organization);
                    Assert.Same(env.TestData.Member, msg2.From);
                    Assert.Equal(TimestampFromTime(env.Clock.UtcNow, "1111"), msg2.ThreadId);
                    Assert.False(msg2.IsLive);
                    Assert.Null(msg2.MessageContext);
                });
        }

        string TimestampFromTime(DateTime utcTime, string suffix)
        {
            var seconds = (int)(utcTime - DateTime.UnixEpoch).TotalSeconds;
            return $"{seconds}.{suffix}";
        }
    }
}
