using Abbot.Common.TestHelpers;
using Microsoft.Bot.Schema;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serious;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Events;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.Payloads;
using Serious.TestHelpers;
using AsyncEnumerableExtensions = MassTransit.Internals.AsyncEnumerableExtensions;

public class MetaBotTests
{
    public class TheOnMessageActivityAsyncMethod
    {
        [Fact]
        public async Task InvokesTheSkillReferencedInMessage()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var skill = new FakeSkill("test-skill");
            env.BuiltinSkillRegistry.AddSkill(skill);
            var room = await env.CreateRoomAsync(name: "some-room", platformRoomId: "C00000010");
            var turnContext = env.CreateTurnContextForMessageEvent(
                text: $"{env.TestData.Abbot.ToMention()} test-skill",
                channel: room.PlatformRoomId);

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.True(skill.OnMessageActivityAsyncCalled);
            Assert.Empty(env.ConversationTracker.UpdatesReceived);
        }

        [Fact]
        public async Task DoesNotInvokeTheSkillFromForeignMember()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var skill = new FakeSkill("test-skill");
            env.BuiltinSkillRegistry.AddSkill(skill);
            var room = await env.CreateRoomAsync(
                managedConversationsEnabled: true,
                name: "some-room",
                platformRoomId: "C00000010");

            var turnContext = env.CreateTurnContextForMessageEvent(
                text: $"{env.TestData.Abbot.ToMention()} test-skill",
                channel: room.PlatformRoomId,
                from: env.TestData.ForeignMember);

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.False(skill.OnMessageActivityAsyncCalled);
        }

        [Fact]
        public async Task DoesNotInvokeTheSkillFromGuest()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var skill = new FakeSkill("test-skill");
            env.BuiltinSkillRegistry.AddSkill(skill);
            var room = await env.CreateRoomAsync(
                managedConversationsEnabled: true,
                name: "some-room",
                platformRoomId: "C00000010");

            var turnContext = env.CreateTurnContextForMessageEvent(
                text: $"{env.TestData.Abbot.ToMention()} test-skill",
                channel: room.PlatformRoomId,
                from: env.TestData.Guest);

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.False(skill.OnMessageActivityAsyncCalled);
        }

        [Fact]
        public async Task DoesNotInvokeTheSkillFromNonAgentInCommunityRoom()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var skill = new FakeSkill("test-skill");
            env.BuiltinSkillRegistry.AddSkill(skill);
            var room = await env.CreateRoomAsync(
                managedConversationsEnabled: true,
                name: "some-room",
                platformRoomId: "C00000010");

            room.Settings = new RoomSettings
            {
                IsCommunityRoom = true
            };

            await env.Db.SaveChangesAsync();
            var turnContext = env.CreateTurnContextForMessageEvent(
                text: $"{env.TestData.Abbot.ToMention()} test-skill",
                channel: room.PlatformRoomId,
                from: env.TestData.Member);

            Assert.False(env.TestData.Member.IsAgent());
            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.False(skill.OnMessageActivityAsyncCalled);
        }

        [Fact]
        public async Task InvokesSkillFromAgentInCommunityRoom()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var skill = new FakeSkill("test-skill");
            env.BuiltinSkillRegistry.AddSkill(skill);
            var room = await env.CreateRoomAsync(
                managedConversationsEnabled: true,
                name: "some-room",
                platformRoomId: "C00000010");

            room.Settings = new RoomSettings
            {
                IsCommunityRoom = true
            };

            await env.Db.SaveChangesAsync();
            var turnContext = env.CreateTurnContextForMessageEvent(
                text: $"{env.TestData.Abbot.ToMention()} test-skill",
                channel: room.PlatformRoomId,
                from: env.TestData.Member);

            Assert.True(env.TestData.Member.IsAgent());
            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.True(skill.OnMessageActivityAsyncCalled);
        }

        [Fact]
        public async Task InvokesUserSkillReferencedInMessage()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var remoteSkillCallSkill = new FakeSkill(RemoteSkillCallSkill.SkillName);
            env.BuiltinSkillRegistry.AddSkill(remoteSkillCallSkill);
            await env.CreateSkillAsync("test-skill");
            var room = await env.CreateRoomAsync(name: "some-room", platformRoomId: "C00000010");
            var turnContext = env.CreateTurnContextForMessageEvent(
                text: $"{env.TestData.Abbot.ToMention()} test-skill foo bar baz",
                channel: room.PlatformRoomId);

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.NotNull(remoteSkillCallSkill.ReceivedMessageContext);
            Assert.Equal(room.Id, remoteSkillCallSkill.ReceivedMessageContext.Room.Id);
            Assert.Equal("test-skill", remoteSkillCallSkill.ReceivedMessageContext.SkillName);
            Assert.Equal("test-skill foo bar baz", remoteSkillCallSkill.ReceivedMessageContext.Arguments.ToString());
            Assert.Empty(env.ConversationTracker.UpdatesReceived);
        }

        [Fact]
        public async Task InvokesUserSkillByMatchingPatternAndUpdatesExistingConversation()
        {
            // If a message is not an attempt to call a skill (aka, it doesn't start with mentioning Abbot, doesn't
            // use the shortcut character, and is not a DM to Abbot), then the message should update the conversation
            // whether or not a skill was called because of a pattern match.
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var remoteSkillCallSkill = new FakeSkill(RemoteSkillCallSkill.SkillName);
            env.BuiltinSkillRegistry.AddSkill(remoteSkillCallSkill);
            var skill = await env.CreateSkillAsync("test-skill");
            await env.Patterns.CreateAsync(
                name: "plaid pattern",
                pattern: "PUG",
                PatternType.Contains,
                caseSensitive: false,
                skill,
                env.TestData.User,
                enabled: true,
                allowExternalCallers: false);

            var room = await env.CreateRoomAsync(managedConversationsEnabled: true,
                name: "some-room",
                platformRoomId: "C00000010");

            var existingConversation = await env.Conversations.CreateAsync(room,
                new MessagePostedEvent
                {
                    MessageId = "23948012.8910",
                    ThreadId = null,
                    MessageUrl = new Uri("https://example.com/messages/9")
                },
                title: "Test Conversation",
                env.TestData.ForeignMember,
                startedAtUtc: env.Clock.UtcNow.AddDays(-1),
                importedOnUtc: null);

            var turnContext = env.CreateTurnContextForMessageEvent(
                text: "GIMMIE DAT PUG",
                channel: room.PlatformRoomId,
                ts: "23948012.8911",
                threadTs: "23948012.8910");

            Assert.Equal(ConversationState.New, existingConversation.State);
            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.NotNull(remoteSkillCallSkill.ReceivedMessageContext);
            Assert.Equal(room.Id, remoteSkillCallSkill.ReceivedMessageContext.Room.Id);
            Assert.Equal("remoteskillcall", remoteSkillCallSkill.ReceivedMessageContext.SkillName);
            Assert.Equal("GIMMIE DAT PUG", remoteSkillCallSkill.ReceivedMessageContext.Arguments.ToString());
            var convo = await env.Conversations.GetConversationByThreadIdAsync("23948012.8910", room);
            Assert.NotNull(convo);
            Assert.Equal(convo.Id, remoteSkillCallSkill.ReceivedMessageContext.Conversation?.Id);
            Assert.Equal(existingConversation.Id, convo.Id);
            Assert.Equal(ConversationState.Waiting, convo.State);
        }

        [Fact]
        public async Task DoesNotInvokesUserSkillFromExternalUserWhenPatternDoesNotAllowExternalCallers()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var foreignMember = env.TestData.ForeignMember;
            var remoteSkillCallSkill = new FakeSkill(RemoteSkillCallSkill.SkillName);
            env.BuiltinSkillRegistry.AddSkill(remoteSkillCallSkill);
            var skill = await env.CreateSkillAsync("test-skill");
            await env.Patterns.CreateAsync(
                name: "plaid pattern",
                pattern: "PUG",
                PatternType.Contains,
                caseSensitive: false,
                skill,
                env.TestData.User,
                enabled: true,
                allowExternalCallers: false);

            var room = await env.CreateRoomAsync(
                managedConversationsEnabled: true,
                name: "some-room",
                platformRoomId: "C00000010");

            var turnContext = env.CreateTurnContextForMessageEvent(
                text: "GIMMIE DAT PUG",
                ts: "8675309.4223",
                channel: room.PlatformRoomId,
                from: foreignMember);

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.Null(remoteSkillCallSkill.ReceivedMessageContext);
            var convo = await env.Conversations.GetConversationByThreadIdAsync("8675309.4223", room);
            Assert.NotNull(convo);
            Assert.Equal("GIMMIE DAT PUG", convo.Title);
            Assert.Equal("8675309.4223", convo.FirstMessageId);
            Assert.Equal(foreignMember.Id, convo.StartedBy.Id);
        }

        [Fact]
        public async Task InvokesUserSkillFromExternalUserByMatchingExternallyCallablePatternAndCreatesConversation()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var foreignMember = env.TestData.ForeignMember;
            var remoteSkillCallSkill = new FakeSkill(RemoteSkillCallSkill.SkillName);
            env.BuiltinSkillRegistry.AddSkill(remoteSkillCallSkill);
            var skill = await env.CreateSkillAsync("test-skill");
            await env.Patterns.CreateAsync(
                name: "plaid pattern",
                pattern: "PUG",
                PatternType.Contains,
                caseSensitive: false,
                skill,
                env.TestData.User,
                enabled: true,
                allowExternalCallers: true);

            var room = await env.CreateRoomAsync(
                managedConversationsEnabled: true,
                name: "some-room",
                platformRoomId: "C00000010");

            var turnContext = env.CreateTurnContextForMessageEvent(
                text: "GIMMIE DAT PUG",
                ts: "8675309.4223",
                channel: room.PlatformRoomId,
                from: foreignMember);

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.NotNull(remoteSkillCallSkill.ReceivedMessageContext);
            Assert.Equal(room.Id, remoteSkillCallSkill.ReceivedMessageContext.Room.Id);
            Assert.Equal("remoteskillcall", remoteSkillCallSkill.ReceivedMessageContext.SkillName);
            Assert.Equal("GIMMIE DAT PUG", remoteSkillCallSkill.ReceivedMessageContext.Arguments.ToString());
            var convo = await env.Conversations.GetConversationByThreadIdAsync("8675309.4223", room);
            Assert.NotNull(convo);
            Assert.Equal(convo.Id, remoteSkillCallSkill.ReceivedMessageContext.Conversation?.Id);
            Assert.Equal("GIMMIE DAT PUG", convo.Title);
            Assert.Equal("8675309.4223", convo.FirstMessageId);
            Assert.Equal(foreignMember.Id, convo.StartedBy.Id);
        }

        [Fact]
        public async Task InvokesUserSkillFromGuestUserByMatchingExternallyCallablePatternAndCreatesConversation()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .ReplaceService<ITurnContextTranslator, TurnContextTranslator>()
                .Build();

            var fromMember = env.TestData.Guest;
            var remoteSkillCallSkill = new FakeSkill(RemoteSkillCallSkill.SkillName);
            env.BuiltinSkillRegistry.AddSkill(remoteSkillCallSkill);
            var skill = await env.CreateSkillAsync("test-skill");
            await env.Patterns.CreateAsync(
                name: "plaid pattern",
                pattern: "PUG",
                PatternType.Contains,
                caseSensitive: false,
                skill,
                env.TestData.User,
                enabled: true,
                allowExternalCallers: true);

            var room = await env.CreateRoomAsync(
                managedConversationsEnabled: true,
                name: "some-room",
                platformRoomId: "C00000010");

            var turnContext = env.CreateTurnContextForMessageEvent(
                text: "GIMMIE DAT PUG",
                ts: "8675309.4223",
                channel: room.PlatformRoomId,
                from: fromMember);

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            Assert.NotNull(remoteSkillCallSkill.ReceivedMessageContext);
            Assert.Equal(room.Id, remoteSkillCallSkill.ReceivedMessageContext.Room.Id);
            Assert.Equal("remoteskillcall", remoteSkillCallSkill.ReceivedMessageContext.SkillName);
            Assert.Equal("GIMMIE DAT PUG", remoteSkillCallSkill.ReceivedMessageContext.Arguments.ToString());
            var convo = await env.Conversations.GetConversationByThreadIdAsync("8675309.4223", room);
            Assert.NotNull(convo);
            Assert.Equal(convo.Id, remoteSkillCallSkill.ReceivedMessageContext.Conversation?.Id);
            Assert.Equal("GIMMIE DAT PUG", convo.Title);
            Assert.Equal("8675309.4223", convo.FirstMessageId);
            Assert.Equal(fromMember.Id, convo.StartedBy.Id);
        }

        [Fact]
        public async Task InvokesSkillUsingCallbackInfo()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ISkillRouter>(out var skillRouter)
                .Substitute<ISkill>(out var skill)
                .Substitute<ITurnContextTranslator>(out var translator)
                .Build();
            var room = await env.CreateRoomAsync(name: "some-room", platformRoomId: "C0000");
            var platformMessage =
                env.CreatePlatformMessage(room: room, callbackInfo: new UserSkillCallbackInfo(new(123)));
            var messageContext = FakeMessageContext.Create(platformMessage);
            var routeResult = new RouteResult(messageContext, skill, true, false);
            skillRouter.RetrieveSkillAsync(Args.PlatformMessage).Returns(routeResult);
            skillRouter.RetrievePayloadHandler(Arg.Any<IPlatformEvent>())
                .Returns(PayloadHandlerRouteResult.Ignore);

            var turnContext = new FakeTurnContext(new Activity
            {
                ChannelId = "unittest",
                Type = "message",
                Text = "<@abbot> test-skill",
                From = new ChannelAccount($"{env.TestData.User.PlatformUserId}:bar", "somebody"),
                Recipient = new ChannelAccount("abbot:abbot", "abbot")
            });


            // We don't pass `turnContext` here because BotFramework wraps the turnContext we pass in with a
            // DelegatingContext.
            translator.TranslateMessageAsync(Args.TurnContext).Returns(platformMessage);

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            await skill.Received()
                .OnMessageActivityAsync(
                    Args.MessageContext,
                    Args.CancellationToken);

            Assert.Empty(env.ConversationTracker.UpdatesReceived);
        }

        [Fact]
        public async Task RespondsWithMessageWhenOrganizationNotEnabledAndMessageDirectedAtAbbot()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Substitute<ISkillRouter>(out var skillRouter)
                .Substitute<ISkill>(out var skill)
                .Build();

            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(room, new UserSkillCallbackInfo(new(23)));
            turnContextTranslator.TranslateMessageAsync(Args.TurnContext).Returns(platformMessage);
            var messageContext = env.CreateFakeMessageContext("test-skill");
            env.TestData.Organization.Enabled = false;
            await env.Db.SaveChangesAsync();
            var routeResult = new RouteResult(messageContext, skill, true, false);
            skillRouter.RetrieveSkillAsync(Args.PlatformMessage).Returns(routeResult);
            var turnContext = new FakeTurnContext(new Activity
            {
                ChannelId = "unittest",
                Type = "message",
                Text = "<@abbot> test-skill",
                From = new ChannelAccount($"{env.TestData.User.PlatformUserId}:bar", "somebody"),
                Recipient = new ChannelAccount("abbot:abbot", "abbot")
            });

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            await skill.DidNotReceive()
                .OnMessageActivityAsync(
                    Args.MessageContext,
                    Args.CancellationToken);

            var reply = Assert.Single(env.Responder.SentMessages);
            Assert.Equal(
                $"Sorry, I cannot do that. Your organization is disabled. Please contact {WebConstants.SupportEmail} for more information.",
                reply.Text);
        }

        [Fact]
        public async Task IgnoresEmptyMessage()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ISkillRouter>(out var router)
                .Build();

            router.RetrieveSkillAsync(Args.PlatformMessage)
                .Throws(new InvalidOperationException("This shouldn't get called!"));

            var bot = env.Activate<MetaBot>();
            var turnContext = new FakeTurnContext(new Activity
            {
                ChannelId = "unittest",
                Type = "message",
                Text = "",
                From = new ChannelAccount("foo:bar", "somebody"),
                Recipient = new ChannelAccount("bot:bot", "abbot")
            });

            await bot.OnTurnAsync(turnContext);
            Assert.Empty(env.ConversationTracker.UpdatesReceived);
        }

        [Fact]
        public async Task IgnoresMessageFromAbbot()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Substitute<ISkillRouter>(out var router)
                .Build();

            var activity = new Activity
            {
                ChannelId = "slack",
                Type = "message",
                Text = "ping",
            };

            var room = await env.CreateRoomAsync("some-room", managedConversationsEnabled: true);
            var platformMessage = new PlatformMessage(
                new MessageEventInfo(
                    "ping",
                    "C001",
                    "U001",
                    Array.Empty<string>(),
                    DirectMessage: true,
                    Ignore: false,
                    MessageId: null,
                    ThreadId: null,
                    null,
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>()
                ),
                null,
                env.TestData.Organization,
                env.Clock.UtcNow,
                new FakeResponder(),
                env.TestData.Abbot,
                BotChannelUser.GetBotUser(env.TestData.Organization),
                Enumerable.Empty<Member>(),
                room);

            // We don't pass `turnContext` here because BotFramework wraps the turnContext we pass in with a
            // DelegatingContext.
            turnContextTranslator.TranslateMessageAsync(Args.TurnContext).Returns(platformMessage);
            var turnContext = new FakeTurnContext(activity);

            router.RetrieveSkillAsync(Args.PlatformMessage)
                .Throws(new InvalidOperationException("This shouldn't get called!"));

            var bot = env.Activate<MetaBot>();
            await bot.OnTurnAsync(turnContext);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task IgnoresMessageIfNotDirectedAtAbbotAndNotInADM(bool orgEnabled)
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Substitute<ISkillNotFoundHandler>(out var handler)
                .Build();

            var organization = env.TestData.Organization;
            organization.Enabled = orgEnabled;
            await env.Db.SaveChangesAsync();
            var foreignMember = env.TestData.ForeignMember;
            var activity = new Activity
            {
                ChannelId = "slack",
                Type = "message",
                Text = $"hey <@U012345> tell me about <@{organization.PlatformBotUserId}>",
            };

            handler.HandleSkillNotFoundAsync(Args.MessageContext)
                .Throws(new InvalidOperationException("This shouldn't get called!"));

            var room = await env.CreateRoomAsync("some-room", managedConversationsEnabled: true);
            var platformMessage = new PlatformMessage(
                new MessageEventInfo(
                    "hey @paul tell me about @abbot",
                    "C001",
                    "U001",
                    Array.Empty<string>(),
                    DirectMessage: false,
                    Ignore: false,
                    MessageId: env.IdGenerator.GetSlackMessageId(),
                    ThreadId: null,
                    null,
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>()
                ),
                null,
                organization,
                env.Clock.UtcNow,
                new FakeResponder(),
                foreignMember,
                BotChannelUser.GetBotUser(organization),
                Enumerable.Empty<Member>(),
                room);

            // We don't pass `turnContext` here because BotFramework wraps the turnContext we pass in with a
            // DelegatingContext.
            turnContextTranslator.TranslateMessageAsync(Args.TurnContext).Returns(platformMessage);
            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(new FakeTurnContext(activity));

            Assert.Empty(handler.ReceivedCalls());
        }

        [Fact]
        public async Task SendsMessageToMagicResponderIfEnabledWhenSkillNotFound()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ISkillRouter>(out var router)
                .Substitute<ITurnContextTranslator>(out var translator)
                .Build();

            env.TestData.Organization.Settings = env.TestData.Organization.Settings with
            {
                AIEnhancementsEnabled = true
            };
            await env.Db.SaveChangesAsync();
            env.Clock.Freeze();
            env.Features.Set(FeatureFlags.MagicResponder, false);
            env.Features.Set(FeatureFlags.MagicResponder, env.TestData.Organization, true);
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var messageContext = env.CreateFakeMessageContext(room: room, threadId: convo.FirstMessageId);
            var result = new RouteResult(messageContext, null, true, false);
            router.RetrieveSkillAsync(Args.PlatformMessage).Returns(result);
            var bot = env.Activate<MetaBot>();
            var turnContext = new FakeTurnContext(new Activity
            {
                ChannelId = "unittest",
                Type = "message",
                Text = "@abbot unknown",
                From = new ChannelAccount("foo:bar", "somebody"),
                Recipient = new ChannelAccount("bot:bot", "abbot")
            });

            var platformMessage = env.CreatePlatformMessage(
                room: room,
                callbackInfo: new UserSkillCallbackInfo(new(123)),
                from: env.TestData.Member,
                payload: new MessageEventInfo(
                    "@abbot unknown",
                    room.PlatformRoomId,
                    env.TestData.User.PlatformUserId,
                    Array.Empty<string>(),
                    false,
                    false,
                    MessageId: "message.id",
                    ThreadId: "thread.id",
                    null,
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>()),
                mentions: new[] { env.TestData.Member, env.TestData.ForeignMember, env.TestData.Abbot });

            // We don't pass `turnContext` here because BotFramework wraps the turnContext we pass in with a
            // DelegatingContext.
            translator.TranslateMessageAsync(Args.TurnContext)
                .Returns(platformMessage);

            await bot.OnTurnAsync(turnContext);

            var published =
                await AsyncEnumerableExtensions.ToListAsync(env.BusTestHarness.Published
                    .SelectAsync<ReceivedChatMessage>());

            var message = Assert.Single(published);
            Assert.Equal("message.id", message.Context.Message.ChatMessage.MessageId);
            Assert.Equal("thread.id", message.Context.Message.ChatMessage.ThreadId);
            Assert.Equal(convo, message.Context.Message.ChatMessage.ConversationId);
            Assert.Equal(new Id<Member>[] { env.TestData.Member, env.TestData.ForeignMember, env.TestData.Abbot },
                message.Context.Message.ChatMessage.MentionedUsers);

            Assert.Equal(env.TestData.Organization, message.Context.Message.ChatMessage.Event.OrganizationId);
            Assert.Equal(env.Clock.UtcNow, message.Context.Message.ChatMessage.Event.Timestamp);
            Assert.Equal(room, message.Context.Message.ChatMessage.Event.RoomId);
            Assert.Equal(env.TestData.Member, message.Context.Message.ChatMessage.Event.SenderId);
        }

        [Fact]
        public async Task DefersToSkillNotFoundHandlerWhenSkillNotFound()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ISkillRouter>(out var router)
                .Substitute<ISkillNotFoundHandler>(out var notFoundHandler)
                .Substitute<ITurnContextTranslator>(out var translator)
                .Build();

            var room = await env.CreateRoomAsync();
            var messageContext = env.CreateFakeMessageContext();
            var result = new RouteResult(messageContext, null, true, false);
            router.RetrieveSkillAsync(Args.PlatformMessage).Returns(result);
            var bot = env.Activate<MetaBot>();
            var turnContext = new FakeTurnContext(new Activity
            {
                ChannelId = "unittest",
                Type = "message",
                Text = "@abbot unknown",
                From = new ChannelAccount("foo:bar", "somebody"),
                Recipient = new ChannelAccount("bot:bot", "abbot")
            });

            var platformMessage = env.CreatePlatformMessage(
                room: room,
                callbackInfo: new UserSkillCallbackInfo(new(123)),
                payload: new MessageEventInfo(
                    "@abbot unknown",
                    room.PlatformRoomId,
                    env.TestData.User.PlatformUserId,
                    Array.Empty<string>(),
                    false,
                    false,
                    MessageId: null,
                    ThreadId: null,
                    null,
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>()));

            // We don't pass `turnContext` here because BotFramework wraps the turnContext we pass in with a
            // DelegatingContext.
            translator.TranslateMessageAsync(Args.TurnContext)
                .Returns(platformMessage);

            await bot.OnTurnAsync(turnContext);

            await notFoundHandler.Received().HandleSkillNotFoundAsync(messageContext);
            Assert.Empty(env.ConversationTracker.UpdatesReceived);
        }

        [Fact]
        public async Task WithNonSkillDirectMessageRespondsWithHelpfulMessage()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Substitute<ISkillRouter>(out var router)
                .Build();

            var messageContext = env.CreateFakeMessageContext(messageId: "1678236563.400000", directMessage: true);
            var result = new RouteResult(messageContext, null, true, false);
            var platformMessage = messageContext.PlatformMessage;
            Assert.False(platformMessage.IsInThread);

            turnContextTranslator.TranslateMessageAsync(Args.TurnContext)
                .Returns(platformMessage);

            router.RetrieveSkillAsync(Args.PlatformMessage).Returns(result);
            var bot = env.Activate<MetaBot>();
            var turnContext = new FakeTurnContext(new Activity
            {
                Type = "message",
                Text = "A direct message"
            });

            await bot.OnTurnAsync(turnContext);

            Assert.Empty(env.ConversationTracker.UpdatesReceived);
            var responder = Assert.IsAssignableFrom<FakeResponder>(platformMessage.Responder);
            var message = Assert.Single(responder.SentMessages);
            Assert.Equal(":wave: Hey! Thanks for the message. What would you like to do next?", message.Text);
        }

        [Fact]
        public async Task WithNonSkillDirectMessageInThreadDoesNotRespond()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Substitute<ISkillRouter>(out var router)
                .Build();
            var room = await env.CreateRoomAsync();
            var threadId = env.IdGenerator.GetSlackMessageId();
            var platformMessage = env.CreatePlatformMessageWithoutInteraction(
                room,
                messageId: env.IdGenerator.GetSlackMessageId(),
                threadId: threadId,
                directMessage: true);
            var messageContext = FakeMessageContext.Create(platformMessage);
            var noSkillResult = new RouteResult(messageContext, Skill: null, IsDirectedAtBot: true, IsPatternMatch: false);
            Assert.True(platformMessage.IsInThread);
            turnContextTranslator.TranslateMessageAsync(Args.TurnContext).Returns(platformMessage);
            router.RetrieveSkillAsync(Args.PlatformMessage).Returns(noSkillResult);
            var bot = env.Activate<MetaBot>();
            var turnContext = new FakeTurnContext(new Activity
            {
                Type = "message",
                Text = "A direct message"
            });

            await bot.OnTurnAsync(turnContext);

            Assert.Empty(env.ConversationTracker.UpdatesReceived);
            var responder = Assert.IsAssignableFrom<FakeResponder>(platformMessage.Responder);
            Assert.Empty(responder.SentMessages);
        }

        [Fact]
        public async Task IgnoresInteractionsInDirectMessages()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Substitute<ISkillRouter>(out var router)
                .Substitute<ISkillNotFoundHandler>(out var notFoundHandler)
                .Build();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(
                room,
                payload: new MessageEventInfo(
                    "A direct message",
                    room.PlatformRoomId,
                    env.TestData.User.PlatformUserId,
                    Array.Empty<string>(),
                    true,
                    false,
                    "12324567.12324",
                    null,
                    new MessageInteractionInfo(new MessageBlockActionsPayload
                    {
                        Container = new MessageContainer("12324567.12324", false, room.PlatformRoomId)
                    },
                    "",
                    new BuiltInSkillCallbackInfo("")),
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>()));
            var messageContext = FakeMessageContext.Create(platformMessage);
            var result = new RouteResult(messageContext, null, true, false);
            turnContextTranslator.TranslateMessageAsync(Args.TurnContext)
                .Returns(platformMessage);
            router.RetrieveSkillAsync(Args.PlatformMessage).Returns(result);
            var bot = env.Activate<MetaBot>();
            var turnContext = new FakeTurnContext(new Activity
            {
                Type = "message",
                Text = "A direct message"
            });

            await bot.OnTurnAsync(turnContext);

            Assert.Empty(env.ConversationTracker.UpdatesReceived);
            var responder = Assert.IsAssignableFrom<FakeResponder>(platformMessage.Responder);
            Assert.Empty(responder.SentMessages);
            await notFoundHandler.DidNotReceive().HandleSkillNotFoundAsync(Args.MessageContext);
        }

        [Fact]
        public async Task DoesNotCallSkillNotFoundHandlerForInteractions()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Substitute<ISkillRouter>(out var router)
                .Substitute<ISkillNotFoundHandler>(out var notFoundHandler)
                .Build();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(
                room,
                payload: new MessageEventInfo(
                    "A direct message",
                    room.PlatformRoomId,
                    env.TestData.User.PlatformUserId,
                    Array.Empty<string>(),
                    true,
                    false,
                    "12324567.12324",
                    null,
                    new MessageInteractionInfo(new MessageBlockActionsPayload
                    {
                        Container = new MessageContainer("12324567.12324", false, room.PlatformRoomId)
                    },
                        "",
                        new BuiltInSkillCallbackInfo("")),
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>())
            );
            var messageContext = FakeMessageContext.Create(platformMessage);
            var result = new RouteResult(messageContext, null, true, false);

            turnContextTranslator.TranslateMessageAsync(Args.TurnContext)
                .Returns(platformMessage);

            router.RetrieveSkillAsync(Args.PlatformMessage).Returns(result);
            var bot = env.Activate<MetaBot>();
            var turnContext = new FakeTurnContext(new Activity
            {
                Type = "message",
                Text = "A direct message"
            });

            await bot.OnTurnAsync(turnContext);

            Assert.Empty(env.ConversationTracker.UpdatesReceived);
            var responder = Assert.IsAssignableFrom<FakeResponder>(platformMessage.Responder);
            Assert.Empty(responder.SentMessages);
            await notFoundHandler.DidNotReceive().HandleSkillNotFoundAsync(Args.MessageContext);
        }

        [Fact]
        public async Task IgnoresMessageMarkedForIgnoringByPlatformHandler()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ISkillRouter>(out var router)
                .Substitute<ISkillNotFoundHandler>(out var handler)
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Build();

            // We don't pass `turnContext` here because BotFramework wraps the turnContext we pass in with a
            // DelegatingContext.
            turnContextTranslator.TranslateMessageAsync(Args.TurnContext)
                .Returns((IPlatformMessage?)null);

            var routeResult = RouteResult.Ignore;
            router.RetrieveSkillAsync(Args.PlatformMessage)
                .Returns(routeResult);

            handler.HandleSkillNotFoundAsync(Args.MessageContext)
                .Throws(new InvalidOperationException("This shouldn't get called!"));

            var bot = env.Activate<MetaBot>();
            var turnContext = new FakeTurnContext(new Activity
            {
                ChannelId = "unittest",
                Type = "message",
                Text = "hey @paul tell me about @abbot",
                From = new ChannelAccount("foo:bar", "somebody"),
                Recipient = new ChannelAccount("bot:bot", "abbot")
            });

            await bot.OnTurnAsync(turnContext);
            Assert.Empty(env.ConversationTracker.UpdatesReceived);
        }

        [Fact]
        public async Task CreatesNewConversationForForeignMessageWithNoConversation()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Build();

            env.OpenAiClient.PushCompletionResult("[!topic:social][!category2:value2]");
            var organization = env.TestData.Organization;
            organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true,
                IgnoreSocialMessages = false,
            };

            await env.Db.SaveChangesAsync();
            var foreignMember = env.TestData.ForeignMember;
            var activity = new Activity
            {
                ChannelId = "slack",
                Type = "message",
                Text = "hey @paul tell me about @abbot"
            };

            var room = await env.CreateRoomAsync("some-room", managedConversationsEnabled: true);
            var platformMessage = new PlatformMessage(
                new MessageEventInfo(
                    "hey @paul tell me about @abbot",
                    "C001",
                    "U001",
                    Array.Empty<string>(),
                    DirectMessage: false,
                    Ignore: false,
                    MessageId: "1678134212.229159",
                    ThreadId: null,
                    null,
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>()
                ),
                null,
                organization,
                env.Clock.UtcNow,
                new FakeResponder(),
                foreignMember,
                BotChannelUser.GetBotUser(organization),
                Enumerable.Empty<Member>(),
                room);

            turnContextTranslator.TranslateMessageAsync(Args.TurnContext).Returns(platformMessage);
            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(new FakeTurnContext(activity));

            var convo = env.ConversationTracker.ThreadIdToConversationMappings["1678134212.229159"];
            Assert.Equal("hey @paul tell me about @abbot", convo.Title);
            Assert.Equal("1678134212.229159", convo.FirstMessageId);
            Assert.Equal(foreignMember.Id, convo.StartedBy.Id);
            var receivedMessage = Assert.Single(env.ConversationTracker.ConversationMessagesReceived);
            Assert.Collection(receivedMessage.Categories,
                c => Assert.Equal("topic:social", c.ToString()),
                c => Assert.Equal("category2:value2", c.ToString()));
        }

        [Fact]
        public async Task DoesNotCreateNewConversationForSocialMessageWithNoConversation()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Build();

            env.OpenAiClient.PushCompletionResult("[!topic:social][!category2:value2]");
            var organization = env.TestData.Organization;
            organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true,
                IgnoreSocialMessages = true
            };

            var foreignMember = env.TestData.ForeignMember;
            var activity = new Activity
            {
                ChannelId = "slack",
                Type = "message",
                Text = "hey @paul tell me about @abbot"
            };

            var room = await env.CreateRoomAsync("some-room", managedConversationsEnabled: true);
            var platformMessage = new PlatformMessage(
                new MessageEventInfo(
                    "hey @paul tell me about @abbot",
                    "C001",
                    "U001",
                    Array.Empty<string>(),
                    DirectMessage: false,
                    Ignore: false,
                    MessageId: "1678134212.229159",
                    ThreadId: null,
                    null,
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>()
                ),
                null,
                organization,
                env.Clock.UtcNow,
                new FakeResponder(),
                foreignMember,
                BotChannelUser.GetBotUser(organization),
                Enumerable.Empty<Member>(),
                room);

            turnContextTranslator.TranslateMessageAsync(Args.TurnContext).Returns(platformMessage);
            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(new FakeTurnContext(activity));

            Assert.Empty(env.ConversationTracker.ThreadIdToConversationMappings);
            Assert.Empty(env.ConversationTracker.ConversationMessagesReceived);
        }

        [Fact]
        public async Task UpdatesExistingConversationForForeignMessageWithExistingConversation()
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .ReplaceService<IConversationRepository, ConversationRepository>()
                .Substitute<ITurnContextTranslator>(out var turnContextTranslator)
                .Build();

            env.OpenAiClient.PushCompletionResult("[!topic:docs][!category2:value2]");
            var organization = env.TestData.Organization;
            organization.Settings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true
            };

            await env.Db.SaveChangesAsync();
            var foreignMember = env.TestData.ForeignMember;
            var activity = new Activity
            {
                ChannelId = "slack",
                Type = "message",
                Text = "sure, I can tell you about Abbot",
            };

            var room = await env.CreateRoomAsync("some-room", managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(
                room,
                firstMessageId: "1678134212.229159",
                initialState: ConversationState.NeedsResponse);

            var platformMessage = new PlatformMessage(
                new MessageEventInfo(
                    "sure, I can tell you about Abbot",
                    "C001",
                    "U001",
                    Array.Empty<string>(),
                    DirectMessage: false,
                    Ignore: false,
                    MessageId: "1678134213.329159",
                    ThreadId: "1678134212.229159",
                    null,
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>()
                ),
                null,
                organization,
                env.Clock.UtcNow,
                new FakeResponder(),
                foreignMember,
                BotChannelUser.GetBotUser(organization),
                Enumerable.Empty<Member>(),
                room);

            turnContextTranslator.TranslateMessageAsync(Args.TurnContext).Returns(platformMessage);
            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(new FakeTurnContext(activity));

            using var _ = env.GetRequiredServiceInNewScope<AbbotContext>(out var dbContext);
            var updatedConversation = await dbContext
                .Conversations
                .Include(c => c.Events)
                .Include(c => c.Tags)
                .SingleAsync(c => c.Id == conversation.Id);

            Assert.Equal(ConversationState.NeedsResponse, updatedConversation.State);
        }
    }

    public class TheOnEventAsyncMethod
    {
        [Fact]
        public async Task TranslatesEventAndInvokesHandler()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var translator)
                .Substitute<ISkillRouter>(out var router)
                .Substitute<IPayloadHandlerInvoker>(out var handlerInvoker)
                .Build();

            var platformEvent = env.CreateFakePlatformEvent(new TeamRenameEvent());
            translator.TranslateEventAsync(Args.TurnContext).Returns(platformEvent);
            handlerInvoker.InvokeAsync(platformEvent).Returns(Task.CompletedTask);
            var result = new PayloadHandlerRouteResult(handlerInvoker);
            router.RetrievePayloadHandler(platformEvent).Returns(result);
            var turnContext = new FakeTurnContext(new Activity
            {
                ChannelId = "unittest",
                Type = ActivityTypes.Event
            });

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            await handlerInvoker.Received().InvokeAsync(platformEvent);
        }
    }

    public class TheOnConversationUpdateActivityAsyncMethod
    {
        [Fact]
        public async Task CallsTheAppHomeHandler()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var translator)
                .Substitute<ISkillRouter>(out var router)
                .Substitute<IPayloadHandlerInvoker>(out var handlerInvoker)
                .Build();

            var platformEvent = env.CreateFakePlatformEvent(new AppHomeOpenedEvent());
            translator.TranslateEventAsync(Args.TurnContext).Returns(platformEvent);
            handlerInvoker.InvokeAsync(platformEvent).Returns(Task.CompletedTask);
            var result = new PayloadHandlerRouteResult(handlerInvoker);
            router.RetrievePayloadHandler(platformEvent).Returns(result);
            var turnContext = new FakeTurnContext(new Activity
            {
                ChannelId = "unittest",
                Type = ActivityTypes.Event
            });

            var bot = env.Activate<MetaBot>();

            await bot.OnTurnAsync(turnContext);

            await handlerInvoker.Received().InvokeAsync(platformEvent);
        }
    }
}
