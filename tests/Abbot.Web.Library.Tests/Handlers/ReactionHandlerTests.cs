using Abbot.Common.TestHelpers;
using Hangfire.States;
using Microsoft.Bot.Schema;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Models;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Security;
using Serious.Abbot.Signals;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;

public class ReactionHandlerTests
{
    public class TheOnPlatformEventAsyncMethod
    {
        public class TestData : CommonTestData
        {
            TestEnvironmentWithData _env = null!;

            public Room Room { get; private set; } = null!;

            public Conversation Conversation { get; private set; } = null!;

            protected override async Task SeedAsync(TestEnvironmentWithData env)
            {
                _env = env;

                await base.SeedAsync(env);

                await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
                Room = await env.CreateRoomAsync(managedConversationsEnabled: true);
                Conversation = await env.CreateConversationAsync(Room, startedBy: env.TestData.ForeignMember);
                env.SlackApi.Conversations.AddConversationHistoryResponse(
                    env.TestData.Organization.RequireAndRevealApiToken(),
                    channel: Room.PlatformRoomId,
                    messages: new[]
                    {
                        new SlackMessage
                        {
                            Text = "Hello, world",
                            ThreadTimestamp = Conversation.FirstMessageId,
                            Timestamp = "9999.9999",
                            User = env.TestData.ForeignUser.PlatformUserId,
                        }
                    });
            }

            public IPlatformEvent<ReactionAddedEvent> CreateReactionEvent(
                string reaction,
                string? timestamp = null,
                Room? room = null,
                Member? actor = null)
            {
                var reactionRoom = room ?? Room;
                return _env.CreateFakePlatformEvent(new ReactionAddedEvent
                {
                    Reaction = reaction,
                    Item = new ReactionItem("message", reactionRoom.PlatformRoomId, timestamp ?? "9999.9999")
                },
                    room: reactionRoom,
                    from: actor);
            }
        }

        [Fact]
        public async Task WithWhiteCheckMarkRaisesSignalAndClosesConversationAndRespondsWithMessage()
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var conversation = env.TestData.Conversation;
            Assert.Equal(ConversationState.New, conversation.State);
            var platformEvent = env.TestData.CreateReactionEvent("white_check_mark");
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Equal(ConversationState.Closed, conversation.State);
            var reply = Assert.IsType<RichActivity>(Assert.Single(env.Responder.SentMessages));
            Assert.Equal("You’ve closed this conversation by replying with ✅.", reply.Text);
            Assert.Equal(env.TestData.Member.User.PlatformUserId, reply.EphemeralUser);
            Assert.Collection(reply.Blocks,
                b => {
                    var messageUrl = env.TestData.Conversation.GetFirstMessageUrl();
                    Assert.Equal($"You’ve closed <{messageUrl}|this conversation> by replying with ✅.",
                        Assert.IsType<MrkdwnText>(Assert.IsType<Section>(b).Text).Text);
                },
                b => {
                    var actionsBlock = Assert.IsType<Actions>(b);
                    Assert.Equal(InteractionCallbackInfo.For<ReactionHandler>(), actionsBlock.BlockId);
                    Assert.Equal(new[]
                        {
                            $"Reopen|{conversation.Id}|",
                            $"Suppress|{conversation.Id}|{EmojiReactionAction.Close}",
                            "dismiss",
                        },
                        actionsBlock.Elements.Cast<ButtonElement>().Select(b => b.Value).ToArray());
                });
            env.SignalHandler.AssertRaised(
                SystemSignal.ReactionAddedSignal.Name,
                "white_check_mark",
                env.TestData.Room.PlatformRoomId,
                env.TestData.Member,
                new MessageInfo(
                    "9999.9999",
                    "Hello, world",
                    new Uri("https://testorg.example.com/archives/C0004/p99999999?thread_ts=1111.0005"),
                    conversation.FirstMessageId,
                    conversation,
                    env.TestData.ForeignMember));
        }

        [Fact]
        public async Task DoesNotRaiseSignalWhenBotAddsReaction()
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var conversation = env.TestData.Conversation;
            Assert.Equal(ConversationState.New, conversation.State);
            var platformEvent = env.TestData.CreateReactionEvent("robot_face", actor: env.TestData.Abbot);
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            env.SignalHandler.AssertNotRaised(SystemSignal.ReactionAddedSignal.Name);
        }

        [Fact]
        public async Task WithWhiteCheckMarkClosesConversationButResponseMessageSuppressedDoesNotSendMessage()
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var platformEvent = env.TestData.CreateReactionEvent("white_check_mark");
            var handler = env.Activate<ReactionHandler>();
            await handler.SuppressEmojiResponseMessageAsync(
                env.Settings,
                EmojiReactionAction.Close,
                env.TestData.Member);

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Equal(ConversationState.Closed, env.TestData.Conversation.State);
            Assert.Empty(env.Responder.SentMessages);
            env.SignalHandler.AssertRaised(
                SystemSignal.ReactionAddedSignal.Name,
                "white_check_mark",
                env.TestData.Room.PlatformRoomId,
                env.TestData.Member,
                new MessageInfo(
                    "9999.9999",
                    "Hello, world",
                    new Uri("https://testorg.example.com/archives/C0004/p99999999?thread_ts=1111.0005"),
                    env.TestData.Conversation.FirstMessageId,
                    env.TestData.Conversation,
                    env.TestData.ForeignMember));
        }

        [Fact]
        public async Task WithWhiteCheckMarkButOrganizationSettingDisabledDoesNothing()
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            await ReactionHandler.SetAllowReactionResponsesSetting(
                env.Settings,
                false,
                env.TestData.User,
                env.TestData.Organization);
            var platformEvent = env.TestData.CreateReactionEvent("white_check_mark");
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Equal(ConversationState.New, env.TestData.Conversation.State);
            // We still raise a signal, we just don't do the special handling of reactions.
            env.SignalHandler.AssertRaised(
                SystemSignal.ReactionAddedSignal.Name,
                "white_check_mark",
                env.TestData.Room.PlatformRoomId,
                env.TestData.Member,
                new MessageInfo(
                    "9999.9999",
                    "Hello, world",
                    new Uri("https://testorg.example.com/archives/C0004/p99999999?thread_ts=1111.0005"),
                    env.TestData.Conversation.FirstMessageId,
                    env.TestData.Conversation,
                    env.TestData.ForeignMember));
        }

        [Fact]
        public async Task WithEyesMovesToSnoozedStateForOneHourAndRespondsWithMessage()
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var now = env.Clock.Freeze();
            var platformEvent = env.TestData.CreateReactionEvent("eyes");
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Equal(ConversationState.Snoozed, env.TestData.Conversation.State);
            var (enqueuedJob, state) = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            var scheduledState = Assert.IsType<ScheduledState>(state);
            // Hangfire doesn't care about our clock so we need to test for a range.
            Assert.True(now.AddMinutes(59) < scheduledState.EnqueueAt && scheduledState.EnqueueAt < now.AddMinutes(61));
            Assert.Equal(typeof(ConversationStateChangeJob), enqueuedJob.Type);
            Assert.Equal(nameof(ConversationStateChangeJob.WakeAsync), enqueuedJob.Method.Name);
            var reply = Assert.IsType<RichActivity>(Assert.Single(env.Responder.SentMessages));
            Assert.Equal("👀 Looks like you’re looking into this conversation. I’ll remind you of it in an hour.", reply.Text);
            Assert.Equal(env.TestData.Member.User.PlatformUserId, reply.EphemeralUser);
            Assert.Collection(reply.Blocks,
                b => {
                    var messageUrl = env.TestData.Conversation.GetFirstMessageUrl();
                    Assert.Equal($"👀 Looks like you’re looking into <{messageUrl}|this conversation>. I’ll remind you of it in an hour.",
                        Assert.IsType<MrkdwnText>(Assert.IsType<Section>(b).Text).Text);
                },
                b => {
                    var actionsBlock = Assert.IsType<Actions>(b);
                    Assert.Equal(InteractionCallbackInfo.For<ReactionHandler>(), actionsBlock.BlockId);
                    Assert.Equal(new[]
                    {
                        $"Suppress|{env.TestData.Conversation.Id}|{EmojiReactionAction.Snooze}", "dismiss"
                    }, actionsBlock.Elements.Cast<ButtonElement>().Select(b => b.Value).ToArray());
                });

            env.SignalHandler.AssertRaised(
                SystemSignal.ReactionAddedSignal.Name,
                "eyes",
                env.TestData.Room.PlatformRoomId,
                env.TestData.Member,
                new MessageInfo(
                    "9999.9999",
                    "Hello, world",
                    new Uri("https://testorg.example.com/archives/C0004/p99999999?thread_ts=1111.0005"),
                    env.TestData.Conversation.FirstMessageId,
                    env.TestData.Conversation,
                    env.TestData.ForeignMember));
        }

        [Theory]
        [InlineData("eyes", true)]
        [InlineData("eyes", false)]
        [InlineData("white_check_mark", true)]
        [InlineData("white_check_mark", false)]
        public async Task WhenResponseReacjiPostedInThreadEphemeralMessageIsInThread(string reacji, bool inThread)
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var room = env.TestData.Room;
            var conversation = env.TestData.Conversation;
            env.Clock.Freeze();
            var expectedTimestamp = inThread
                ? "9999.9999"
                : conversation.FirstMessageId;
            var platformEvent = env.TestData.CreateReactionEvent(
                reacji,
                timestamp: expectedTimestamp);
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                env.TestData.Organization.RequireAndRevealApiToken(),
                channel: room.PlatformRoomId,
                messages: new[]
                {
                    new SlackMessage
                    {
                        ThreadTimestamp = conversation.FirstMessageId,
                        Timestamp = "9999.9999",
                        User = env.TestData.ForeignUser.PlatformUserId,
                        Text = "Hello, world"
                    },
                    new SlackMessage
                    {
                        ThreadTimestamp = conversation.FirstMessageId,
                        Timestamp = conversation.FirstMessageId,
                        User = env.TestData.ForeignUser.PlatformUserId,
                        Text = "Hello, world"
                    }
                });
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            var activity = Assert.Single(env.Responder.SentMessages);

            var expected = inThread
                ? $"Room/{room.PlatformRoomId}(THREAD:{conversation.FirstMessageId})"
                : $"Room/{room.PlatformRoomId}";

            Assert.Equal(expected, activity.GetOverriddenDestination()?.Address.ToString());
            env.SignalHandler.AssertRaised(
                SystemSignal.ReactionAddedSignal.Name,
                reacji,
                env.TestData.Room.PlatformRoomId,
                env.TestData.Member,
                new MessageInfo(
                    expectedTimestamp,
                    "Hello, world",
                    new Uri($"https://testorg.example.com/archives/C0004/p{expectedTimestamp.Replace(".", "")}?thread_ts=1111.0005"),
                    env.TestData.Conversation.FirstMessageId,
                    env.TestData.Conversation,
                    env.TestData.ForeignMember));
        }

        [Fact]
        public async Task WithMessageInThreadWithWhiteCheckMarkClosesConversation()
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                env.TestData.Organization.ApiToken?.Reveal()!,
                channel: env.TestData.Room.PlatformRoomId,
                new[]
                {
                    new SlackMessage
                    {
                        Timestamp = "message-id",
                        ThreadTimestamp = env.TestData.Conversation.FirstMessageId,
                        User = env.TestData.ForeignUser.PlatformUserId,
                        Text = "Goodbye, world."
                    }
                });
            var platformEvent = env.TestData.CreateReactionEvent("white_check_mark", "message-id");
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Equal(ConversationState.Closed, env.TestData.Conversation.State);
            env.SignalHandler.AssertRaised(
                SystemSignal.ReactionAddedSignal.Name,
                "white_check_mark",
                env.TestData.Room.PlatformRoomId,
                env.TestData.Member,
                new MessageInfo(
                    "message-id",
                    "Goodbye, world.",
                    new Uri("https://testorg.example.com/archives/C0004/pmessage-id?thread_ts=1111.0005"),
                    env.TestData.Conversation.FirstMessageId,
                    env.TestData.Conversation,
                    env.TestData.ForeignMember));
        }

        [Fact]
        public async Task WithNonWhiteCheckMarkDoesNotChangeState()
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var platformEvent = env.TestData.CreateReactionEvent("laughing");

            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Equal(ConversationState.New, env.TestData.Conversation.State);
            env.SignalHandler.AssertRaised(
                SystemSignal.ReactionAddedSignal.Name,
                "laughing",
                env.TestData.Room.PlatformRoomId,
                env.TestData.Member,
                new MessageInfo(
                    "9999.9999",
                    "Hello, world",
                    new Uri("https://testorg.example.com/archives/C0004/p99999999?thread_ts=1111.0005"),
                    env.TestData.Conversation.FirstMessageId,
                    env.TestData.Conversation,
                    env.TestData.ForeignMember));
        }

        [Theory]
        [InlineData("eyes")]
        [InlineData("white_check_mark")]
        public async Task OnUntrackedConversationDoesNotChangeState(string reaction)
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var firstMessageId = env.IdGenerator.GetSlackMessageId();
            var platformEvent = env.TestData.CreateReactionEvent(reaction, firstMessageId);
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                env.TestData.Organization.RequireAndRevealApiToken(),
                channel: env.TestData.Room.PlatformRoomId,
                messages: new[]
                {
                    new SlackMessage
                    {
                        Timestamp = firstMessageId,
                    }
                });
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Null(await env.Conversations.GetConversationByThreadIdAsync(firstMessageId, env.TestData.Room));
        }

        [Theory]
        [InlineData("eyes")]
        [InlineData("white_check_mark")]
        public async Task InUntrackedRoomDoesNotChangeState(string reaction)
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            var untrackedRoom = await env.CreateRoomAsync(managedConversationsEnabled: false);
            var conversation = await env.CreateConversationAsync(untrackedRoom, startedBy: env.TestData.ForeignMember);
            var platformEvent = env.TestData.CreateReactionEvent(
                reaction,
                room: untrackedRoom);
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                env.TestData.Organization.RequireAndRevealApiToken(),
                channel: untrackedRoom.PlatformRoomId,
                messages: new[]
                {
                    new SlackMessage
                    {
                        ThreadTimestamp = conversation.FirstMessageId,
                        Timestamp = "9999.9999",
                    }
                });
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Equal(ConversationState.New, conversation.State);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WhenNonAgentReactsDoesNotChangeState(bool guest)
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();

            var actor = guest ? env.TestData.Guest : env.TestData.ForeignMember;
            var platformEvent = env.TestData.CreateReactionEvent("white_check_mark", actor: actor);
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Equal(ConversationState.New, env.TestData.Conversation.State);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(false, null)]
        [InlineData(true, false)]
        public async Task WhenTicketReacjiDisabledDoesNotPostEphemeralMessage(bool? orgSetting, bool? roomSetting)
        {
            var env = TestEnvironmentBuilder
                .Create<TestData>()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();
            if (orgSetting is not null)
            {
                await env.Settings.SetAsync(
                    SettingsScope.Organization(env.TestData.Organization),
                    ReactionHandler.AllowTicketReactionSettingName,
                    orgSetting.Value.ToString(),
                    env.TestData.User);
            }
            if (roomSetting is not null)
            {
                await env.Settings.SetAsync(
                    SettingsScope.Room(env.TestData.Room),
                    ReactionHandler.AllowTicketReactionSettingName,
                    roomSetting.Value.ToString(),
                    env.TestData.User);
            }
            var platformEvent = env.TestData.CreateReactionEvent("ticket", actor: env.TestData.ForeignMember);
            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.Empty(env.Responder.SentMessages);
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(false, true)]
        public async Task WhenTicketReacjiEnabledPostsEphemeralMessageForTicketingSystem(bool? orgSetting, bool? roomSetting)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: false);

            await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            if (orgSetting is not null)
            {
                await env.Settings.SetAsync(
                    SettingsScope.Organization(env.TestData.Organization),
                    ReactionHandler.AllowTicketReactionSettingName,
                    orgSetting.Value.ToString(),
                    env.TestData.User);
            }

            if (roomSetting is not null)
            {
                await env.Settings.SetAsync(
                    SettingsScope.Room(room),
                    ReactionHandler.AllowTicketReactionSettingName,
                    roomSetting.Value.ToString(),
                    env.TestData.User);
            }

            var conversation = await env.CreateConversationAsync(room, startedBy: env.TestData.ForeignMember);
            var platformEvent = env.CreateFakePlatformEvent(new ReactionAddedEvent
            {
                Reaction = "ticket",
                Item = new ReactionItem("message", room.PlatformRoomId, "9999.9999")
            },
                from: env.TestData.ForeignMember,
                room: room);
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                env.TestData.Organization.RequireAndRevealApiToken(),
                room.PlatformRoomId,
                new[]
                {
                    new SlackMessage
                    {
                        ThreadTimestamp = conversation.FirstMessageId,
                        Timestamp = "9999.9999",
                    }
                });

            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            var activity = Assert.IsType<RichActivity>(Assert.Single(env.Responder.SentMessages));
            Assert.Equal(env.TestData.ForeignMember.User.PlatformUserId, activity.EphemeralUser);
            Assert.Equal("Please select an action.", activity.Text);

            var txt = Assert.IsType<MrkdwnText>(Assert.IsType<Section>(activity.Blocks.First()).Text);
            Assert.Equal($"Please select an action to take on <{conversation.GetFirstMessageUrl()}|this conversation>.", txt.Text);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WhenTicketReacjiPostedInThreadEphemeralMessageIsInThreadOtherwiseItsTopLevel(bool inThread)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await ReactionHandler.SetAllowTicketReactionSetting(env.Settings,
                true,
                env.TestData.User,
                env.TestData.Organization);

            var conversation = await env.CreateConversationAsync(room, startedBy: env.TestData.ForeignMember);
            var platformEvent = env.CreateFakePlatformEvent(new ReactionAddedEvent
            {
                Reaction = "ticket",
                Item = new ReactionItem("message", room.PlatformRoomId, inThread ? "9999.9999" : conversation.FirstMessageId)
            },
                from: env.TestData.ForeignMember,
                room: room);
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                env.TestData.Organization.RequireAndRevealApiToken(),
                room.PlatformRoomId,
                new[]
                {
                    new SlackMessage
                    {
                        ThreadTimestamp = conversation.FirstMessageId,
                        Timestamp = "9999.9999",
                    },
                    new SlackMessage
                    {
                        ThreadTimestamp = conversation.FirstMessageId,
                        Timestamp = conversation.FirstMessageId,
                    }
                });

            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            var activity = Assert.Single(env.Responder.SentMessages);
            Assert.Equal(inThread
                ? $"Room/{room.PlatformRoomId}(THREAD:{conversation.FirstMessageId})"
                : $"Room/{room.PlatformRoomId}", activity.GetOverriddenDestination()?.Address.ToString());
        }

        [Fact]
        public async Task EncodesConversationInfoInTicketButton()
        {
            // The reaction handler doesn't create a Conversation if one doesn't exist before routing to the
            // specific ticket handler. Instead, it lets the ticket handler handle all that.
            // This test ensures that the conversation info is encoded in the button value (aka "{channel}:{ts}" so
            // that the ticket handler can do the right thing.
            var env = TestEnvironment.Create();

            await ReactionHandler.SetAllowTicketReactionSetting(env.Settings,
                true,
                env.TestData.User,
                env.TestData.Organization);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            var integration = await env.Integrations.EnableAsync(env.TestData.Organization,
                IntegrationType.Zendesk,
                env.TestData.Member);

            var platformEvent = env.CreateFakePlatformEvent(new ReactionAddedEvent
            {
                Reaction = "ticket",
                Item = new ReactionItem("message", room.PlatformRoomId, "9999.9999")
            },
                from: env.TestData.ForeignMember,
                room: room);
            await SetThreadIdAsync(env, room.PlatformRoomId, "9999.9999", "9999.8888");

            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            var activity = Assert.IsType<RichActivity>(Assert.Single(env.Responder.SentMessages));
            Assert.Equal(env.TestData.ForeignMember.User.PlatformUserId, activity.EphemeralUser);

            var actions = Assert.IsType<Actions>(activity.Blocks.Skip(1).Single());
            var buttons = actions.Elements.Cast<ButtonElement>().ToList();
            Assert.Equal(2, buttons.Count);

            Assert.Equal($"{room.PlatformRoomId}:{9999.8888}", buttons[0].Value);
            Assert.Equal($"i:{nameof(CreateZendeskTicketFormModal)}:{integration.Id}", buttons[0].ActionId);
        }

        [Theory]
        [InlineData(false, false, false, false, false, false, new string[0], new string[0], new string[0])]
        [InlineData(true, false, false, false, false, false, new[] { "Create Zendesk Ticket", "Dismiss" }, new[] { $"i:{nameof(CreateZendeskTicketFormModal)}:1", $"i:{nameof(DismissHandler)}" }, new[] { "CONVOID", "dismiss" })]
        [InlineData(true, true, false, false, false, false, new[] { "Create Zendesk Ticket", "Create HubSpot Ticket", "Dismiss" }, new[] { $"i:{nameof(CreateZendeskTicketFormModal)}:1", $"i:{nameof(CreateHubSpotTicketFormModal)}:2", $"i:{nameof(DismissHandler)}" }, new[] { "CONVOID", "CONVOID", "dismiss" })]
        [InlineData(true, true, true, false, false, false, new[] { "Create Zendesk Ticket", "Create HubSpot Ticket", "Create MergeDev Ticket", "Dismiss" }, new[] { $"i:{nameof(CreateZendeskTicketFormModal)}:1", $"i:{nameof(CreateHubSpotTicketFormModal)}:2", $"i:{nameof(CreateMergeDevTicketFormModal)}:3", $"i:{nameof(DismissHandler)}" }, new[] { "CONVOID", "CONVOID", "CONVOID", "dismiss" })]
        [InlineData(true, true, true, true, false, false, new[] { "Create HubSpot Ticket", "Create MergeDev Ticket", "Dismiss" }, new[] { $"i:{nameof(CreateHubSpotTicketFormModal)}:2", $"i:{nameof(CreateMergeDevTicketFormModal)}:3", $"i:{nameof(DismissHandler)}" }, new[] { "CONVOID", "CONVOID", "dismiss" })]
        [InlineData(true, true, true, true, true, false, new[] { "Create MergeDev Ticket", "Dismiss" }, new[] { $"i:{nameof(CreateMergeDevTicketFormModal)}:3", $"i:{nameof(DismissHandler)}" }, new[] { "CONVOID", "dismiss" })]
        [InlineData(true, true, true, true, true, true, new string[0], new string[0], new string[0])]
        public async Task WhenTicketReacjiEnabledEphemeralMessageForExistingConversationContainsButtonsForEachTicketingSystem(
            bool zendeskEnabled,
            bool hubspotEnabled,
            bool ticketingEnabled,
            bool existingZendeskTicket,
            bool existingHubSpotTicket,
            bool existingGenericTicket,
            string[] buttonTexts,
            string[] buttonActionIds,
            string[] buttonValues)
        {
            // Unlike the previous test, in this test the conversation already exists, so the create ticket button
            // values will be the conversation ID and not the channel ID and message ID.
            var env = TestEnvironment.Create();

            await ReactionHandler.SetAllowTicketReactionSetting(env.Settings,
                true,
                env.TestData.User,
                env.TestData.Organization);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room, startedBy: env.TestData.ForeignMember);

            if (zendeskEnabled)
            {
                await env.Integrations.EnableAsync(env.TestData.Organization,
                    IntegrationType.Zendesk,
                    env.TestData.Member);

                if (existingZendeskTicket)
                {
                    var ticketLink = new ZendeskTicketLink("test", 42);
                    await env.CreateConversationLinkAsync(conversation,
                        ConversationLinkType.ZendeskTicket,
                        ticketLink.ToString());
                }
            }

            if (hubspotEnabled)
            {
                await env.Integrations.EnableAsync(env.TestData.Organization,
                    IntegrationType.HubSpot,
                    env.TestData.Member);

                if (existingHubSpotTicket)
                {
                    var ticketLink = new HubSpotTicketLink(42, "1234");
                    await env.CreateConversationLinkAsync(conversation,
                        ConversationLinkType.HubSpotTicket,
                        ticketLink.ToString());
                }
            }

            if (ticketingEnabled)
            {
                var integration = await env.Integrations.EnableAsync(env.TestData.Organization,
                    IntegrationType.Ticketing,
                    env.TestData.Member);
                await env.Integrations.SaveSettingsAsync(integration,
                    new TicketingSettings
                    {
                        AccountDetails = new() { Integration = "MergeDev" },
                    });

                if (existingGenericTicket)
                {
                    await env.CreateConversationLinkAsync(conversation,
                        ConversationLinkType.MergeDevTicket,
                        "ignored-id",
                        new MergeDevTicketLink.Settings(
                            integration,
                            "ignored-slug",
                            "ignored-name",
                            "https://example.com/ignored"));
                }
            }

            var platformEvent = env.CreateFakePlatformEvent(new ReactionAddedEvent
            {
                Reaction = "ticket",
                Item = new ReactionItem("message", room.PlatformRoomId, "9999.9999")
            },
                from: env.TestData.ForeignMember,
                room: room);
            await SetThreadIdAsync(env, room.PlatformRoomId, "9999.9999", conversation.FirstMessageId);

            var handler = env.Activate<ReactionHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            if (buttonTexts.Length == 0)
            {
                Assert.Empty(env.Responder.SentMessages);
            }
            else
            {
                var activity = Assert.IsType<RichActivity>(Assert.Single(env.Responder.SentMessages));
                Assert.Equal(env.TestData.ForeignMember.User.PlatformUserId, activity.EphemeralUser);

                var actions = Assert.IsType<Actions>(activity.Blocks.Skip(1).Single());
                var buttons = actions.Elements.Cast<ButtonElement>().ToList();
                var expectedButtonValues = buttonValues.Select(v => v.Replace("CONVOID", $"{conversation.Id}"))
                    .ToArray();

                var actualButtonValues = buttons.Select(b => b.Value).ToArray();

                Assert.Equal(buttonActionIds, buttons.Select(b => b.ActionId).ToArray());
                Assert.Equal(buttonTexts, buttons.Select(b => b.Text.Text).ToArray());
                Assert.Equal(expectedButtonValues, actualButtonValues);
            }
        }

        async Task SetThreadIdAsync(TestEnvironmentWithData env, string platformRoomId, string messageTs, string threadTs)
        {
            env.SlackApi.Conversations.AddConversationHistoryResponse(
                env.TestData.Organization.RequireAndRevealApiToken(),
                platformRoomId,
                new[]
                {
                    new SlackMessage
                    {
                        ThreadTimestamp = threadTs,
                        Timestamp = messageTs,
                    }
                });
        }
    }

    public class TheOnMessageInteractionAsyncMethod
    {
        [Fact]
        public async Task WhenClickingReopenMovesMessageToOpenState()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();

            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            await env.Conversations.CloseAsync(conversation, env.TestData.Member, env.Clock.UtcNow);
            var interactionInfo = new MessageInteractionInfo(
                new InteractiveMessagePayload
                {
                    ResponseUrl = new Uri("https://example.com/response")
                },
                $"Reopen|{conversation.Id}|",
                InteractionCallbackInfo.For<ReactionHandler>());

            var platformMessage = env.CreatePlatformMessage(room: room, interactionInfo: interactionInfo);
            Assert.Equal(ConversationState.Closed, conversation.State);
            var handler = env.Activate<ReactionHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            var responseUrl = Assert.Single(env.Responder.DeletedMessagesByResponseUrl);
            Assert.Equal(new Uri("https://example.com/response"), responseUrl);
            Assert.False(await ReactionHandler.IsEmojiResponseMessageSuppressedAsync(env.Settings,
                EmojiReactionAction.Close,
                env.TestData.Member));

            Assert.False(await ReactionHandler.IsEmojiResponseMessageSuppressedAsync(env.Settings,
                EmojiReactionAction.Snooze,
                env.TestData.Member));

            Assert.Equal(ConversationState.NeedsResponse, conversation.State);
        }

        [Theory]
        [InlineData("white_check_mark", true, false)]
        [InlineData("eyes", false, true)]
        public async Task WhenClickingNeverShowAgainSuppressesFutureMessages(
            string emojiName,
            bool closeEmojiReactionSuppressed,
            bool snoozeEmojiReactionSuppressed)
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationTracker, ConversationTracker>()
                .Build();

            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var reactionAction = ReactionHandler.GetEmojiReactionActionFromEmojiName(emojiName);
            var interactionInfo = new MessageInteractionInfo(
                new InteractiveMessagePayload
                {
                    ResponseUrl = new Uri("https://example.com/response")
                },
                $"Suppress||{reactionAction}",
                InteractionCallbackInfo.For<ReactionHandler>());

            var platformMessage = env.CreatePlatformMessage(room: room, interactionInfo: interactionInfo);
            var handler = env.Activate<ReactionHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            var responseUrl = Assert.Single(env.Responder.DeletedMessagesByResponseUrl);
            Assert.Equal(new Uri("https://example.com/response"), responseUrl);
            Assert.Equal(closeEmojiReactionSuppressed,
                await ReactionHandler.IsEmojiResponseMessageSuppressedAsync(env.Settings,
                    EmojiReactionAction.Close,
                    env.TestData.Member));

            Assert.Equal(snoozeEmojiReactionSuppressed,
                await ReactionHandler.IsEmojiResponseMessageSuppressedAsync(env.Settings,
                    EmojiReactionAction.Snooze,
                    env.TestData.Member));
        }
    }
}
