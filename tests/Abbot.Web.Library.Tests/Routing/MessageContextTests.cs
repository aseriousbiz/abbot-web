using Abbot.Common.TestHelpers;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Slack.Events;
using Serious.TestHelpers;

public class MessageContextTests
{
    public class TheConstructor
    {
        [Fact]
        public void SetsMessageIdForSlackMessagesFromSlackPlatformMessage()
        {
            var incomingEvent = new EventEnvelope<MessageEvent>
            {
                Event = new MessageEvent
                {
                    Channel = "C0123457",
                    User = "U00111111",
                    Timestamp = "8675309"
                }
            };

            var messageEventPayload = MessageEventInfo.FromSlackMessageEvent(
                string.Empty,
                incomingEvent.Event,
                "U012345");
            Assert.NotNull(messageEventPayload);
            var organization = new Organization
            {
                PlatformId = "T013108BYLS",
                PlatformType = PlatformType.Slack,
                PlatformBotUserId = "B00123",
                PlatformBotId = "B00123"
            };

            var message = new PlatformMessage(
                messageEventPayload,
                null,
                organization,
                DateTimeOffset.UtcNow,
                new FakeResponder(),
                new Member { Organization = organization, User = new User { PlatformUserId = "U012345" } },
                BotChannelUser.GetBotUser(organization),
                Enumerable.Empty<Member>(),
                new Room { PlatformRoomId = "C01234", Name = "some-room", Persistent = true });
            var messageContext = new MessageContext(
                message,
                "skill-name",
                "",
                "skill-name",
                ".skill-name",
                string.Empty,
                Array.Empty<SkillPattern>(),
                null);
            Assert.Equal("8675309", messageContext.MessageId);
        }

        [Fact]
        public async Task IncludesMentionArguments()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var mentioned = await env.CreateMemberAsync("U012LKJFG0P", displayName: "Phil Haack");
            var platformMessage = env.CreatePlatformMessage(room, mentions: new[] { mentioned });

            var messageContext = new MessageContext(
                platformMessage,
                "whois",
                "<@U012LKJFG0P>",
                "whois <@U012LKJFG0P>",
                ".whois <@U012LKJFG0P>",
                string.Empty,
                Array.Empty<SkillPattern>(),
                null);

            Assert.Equal("whois", messageContext.SkillName);
            var mentionArg = Assert.IsAssignableFrom<IMentionArgument>(messageContext.Arguments[0]);
            Assert.Equal("Phil Haack", mentionArg.Mentioned.Name);
            Assert.Equal("U012LKJFG0P", mentionArg.Mentioned.Id);
        }
    }

    public class TheWithResolvedSkillMethod
    {
        [Fact]
        public async Task ReturnsMessageContextWithResolvedSkill()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(room);
            var messageContext = new MessageContext(
                platformMessage,
                "whois",
                "<@U012LKJFG0P>",
                "whois <@U012LKJFG0P>",
                "<@U013WCHH9NU> whois <@U012LKJFG0P>",
                string.Empty,
                Array.Empty<SkillPattern>(),
                null);
            var resolvedSkill = new FakeResolvedSkill
            {
                Name = "some-skill",
                Arguments = "",
                Description = "It's some skill",
                Skill = new FakeSkill("some-skill")
            };

            var result = messageContext.WithResolvedSkill(resolvedSkill);

            Assert.Equal("whois <@U012LKJFG0P>", result.CommandText);
            Assert.Equal("whois", result.SkillName);
            Assert.Equal("<@U012LKJFG0P>", result.Arguments.Value);
            Assert.Equal("<@U012LKJFG0P>", result.Arguments[0].Value);
        }

        [Fact]
        public async Task PrependsSkillArguments()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(room);
            var messageContext = new MessageContext(
                platformMessage,
                "whois",
                "<@U012LKJFG0P>",
                "whois <@U012LKJFG0P>",
                ".whois <@U012LKJFG0P>>",
                string.Empty,
                Array.Empty<SkillPattern>(),
                null);
            var resolvedSkill = new FakeResolvedSkill
            {
                Name = "some-skill",
                Arguments = "prepend",
                Description = "It's some skill",
                Skill = new FakeSkill("some-skill")
            };

            var result = messageContext.WithResolvedSkill(resolvedSkill);

            Assert.Equal("whois <@U012LKJFG0P>", result.CommandText);
            Assert.Equal("whois", result.SkillName);
            Assert.Equal("prepend <@U012LKJFG0P>", result.Arguments.Value);
            Assert.Equal("prepend", result.Arguments[0].Value);
            Assert.Equal("<@U012LKJFG0P>", result.Arguments[1].Value);
        }

        [Fact]
        public async Task PrependsMultipleSkillArguments()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(room);
            var messageContext = new MessageContext(
                platformMessage,
                "whois",
                "<@U012LKJFG0P>",
                "whois <@U012LKJFG0P>",
                ".whois <@U012LKJFG0P>",
                string.Empty,
                Array.Empty<SkillPattern>(),
                null);
            var resolvedSkill = new FakeResolvedSkill
            {
                Name = "some-skill",
                Arguments = "prepend some args",
                Description = "It's some skill",
                Skill = new FakeSkill("some-skill")
            };

            var result = messageContext.WithResolvedSkill(resolvedSkill);

            Assert.Equal("whois <@U012LKJFG0P>", result.CommandText);
            Assert.Equal("whois", result.SkillName);
            Assert.Equal("prepend some args <@U012LKJFG0P>", result.Arguments.Value);
            Assert.Equal("prepend", result.Arguments[0].Value);
            Assert.Equal("some", result.Arguments[1].Value);
            Assert.Equal("args", result.Arguments[2].Value);
            Assert.Equal("<@U012LKJFG0P>", result.Arguments[3].Value);
        }
    }

    public class TheSendActivityAsyncMethod
    {
        [Fact]
        public async Task DoesNotSendInThreadIfInThreadFalseAndOriginalMessageNotInThread()
        {
            // Note: 'threadId' is the value in PlatformMessage that control IsInThread
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(
                room,
                messageId: "1678236563.400000",
                threadId: null);
            var messageContext = new MessageContext(
                platformMessage,
                "ping",
                "",
                "ping",
                ".ping",
                string.Empty,
                Array.Empty<SkillPattern>(),
                null);

            await messageContext.SendActivityAsync("Test", inThread: false);

            Assert.Collection(env.Responder.SentMessages,
                message => {
                    Assert.Equal("Test", message.Text);
                    Assert.IsNotType<AbbotChannelData>(message.ChannelData);
                });
        }

        [Fact]
        public async Task SendsInThreadIfInThreadTrueAndOriginalMessageNotInThread()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(
                room,
                messageId: "1678236563.400000",
                threadId: null);

            var messageContext = new MessageContext(
                platformMessage,
                "ping",
                "",
                "ping",
                ".ping",
                string.Empty,
                Array.Empty<SkillPattern>(),
                null);

            await messageContext.SendActivityAsync("Test", inThread: true);

            Assert.False(messageContext.IsInThread);
            Assert.NotNull(messageContext.ReplyInThreadMessageTarget);
            var message = Assert.Single(env.Responder.SentMessages);
            Assert.Equal("Test", message.Text);
            AbbotChannelData channelData = Assert.IsType<AbbotChannelData>(message.ChannelData);
            Assert.Same(platformMessage.ReplyInThreadMessageTarget, channelData.OverriddenMessageTarget);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SendsInThreadRegardlessOfInThreadIfOriginalMessageIsInThread(bool inThread)
        {
            // Note: 'threadId' is the value in PlatformMessage that control IsInThread
            const string ThreadId = "12345678.333333";
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(room, threadId: ThreadId);
            var messageContext = new MessageContext(
                platformMessage,
                "ping",
                "",
                "ping",
                ".ping",
                string.Empty,
                Array.Empty<SkillPattern>(),
                null);

            await messageContext.SendActivityAsync("Test", inThread);

            Assert.True(messageContext.IsInThread);
            Assert.NotNull(messageContext.ReplyInThreadMessageTarget);
            var message = Assert.Single(env.Responder.SentMessages);
            Assert.Equal("Test", message.Text);
            AbbotChannelData channelData = Assert.IsType<AbbotChannelData>(message.ChannelData);
            Assert.Equal(ThreadId, channelData.OverriddenMessageTarget?.Address.ThreadId);
        }
    }
}
