using Abbot.Common.TestHelpers;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework;
using Serious.Slack.Events;
using Serious.Slack.Payloads;
using Serious.TestHelpers;

public class TurnContextTranslatorTests
{
    public class TheTranslateMessageAsyncMethod
    {
        public class TestData : CommonTestData
        {
            protected override async Task SeedAsync(TestEnvironmentWithData env)
            {
                var organization = env.TestData.Organization;
                organization.PlatformId = "T00012121";
                organization.PlatformBotId = "B012345678";
                organization.PlatformBotUserId = "U012345678";
                organization.BotName = "TheAbbot";
                var foreignOrganization = env.TestData.ForeignOrganization;
                foreignOrganization.PlatformId = "T000998899";
                await env.Db.SaveChangesAsync();
                var apiToken = organization.RequireAndRevealApiToken();
                env.SlackApi.Conversations.AddConversationInfoResponse(
                    apiToken,
                    new ConversationInfo
                    {
                        Id = "C00000001",
                        Name = "general",
                        IsChannel = true,
                        IsGroup = true,
                    });
                env.SlackApi.Conversations.AddConversationInfoResponse(
                    apiToken,
                    new ConversationInfo
                    {
                        Id = "C00000002",
                        Name = "support-room",
                        IsChannel = true,
                        IsGroup = true,
                        IsShared = true,
                        IsExternallyShared = true,
                    });

                env.SlackApi.AddUserInfoResponse(
                    apiToken,
                    new UserInfo
                    {
                        TeamId = organization.PlatformId,
                        Id = env.TestData.User.PlatformUserId,
                        Name = env.TestData.User.DisplayName
                    });

                env.SlackApi.AddUserInfoResponse(apiToken,
                    new UserInfo
                    {
                        Id = "U00000001",
                        TeamId = organization.PlatformId,
                        Name = "Bojack"
                    });

                env.SlackApi.AddUserInfoResponse(apiToken,
                    new UserInfo
                    {
                        Id = "U00000002",
                        TeamId = organization.PlatformId,
                        Name = "Diane"
                    });

                env.SlackApi.AddUserInfoResponse(apiToken,
                    new UserInfo
                    {
                        Id = "U00000042",
                        TeamId = organization.PlatformId,
                        Name = "Marvin"
                    });

                env.SlackApi.AddUserInfoResponse(apiToken,
                    new UserInfo
                    {
                        Id = "U00000023",
                        TeamId = organization.PlatformId,
                        Name = "Jordan"
                    });

                env.SlackApi.AddUserInfoResponse(apiToken,
                    new UserInfo
                    {
                        Id = "U000000F11",
                        TeamId = foreignOrganization.PlatformId,
                        Name = "Supportee"
                    });
            }
        }

        [Fact]
        public async Task CreatesMessageFromRealJson()
        {
            var turnContext = await DeserializeTurnContext("message.with-files.json");
            var env = TestEnvironment.Create<TestData>();

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.False(message.Payload.Ignore); // Abbot not mentioned.
            Assert.Equal("T00012121", message.Organization.PlatformId);
            Assert.Equal("U00000042", message.From.User.PlatformUserId);
            Assert.NotNull(message.Room);
            Assert.Equal("C00000001", message.Room.PlatformRoomId);
            Assert.Equal("general", message.Room.Name);
        }

        [Fact]
        public async Task CreatesMessageFromSharedChannelWithWrongTeamId()
        {
            // We've noticed messages from shared channels sometimes have the wrong team_id on the the
            // envelope. So we'll use the Authorizations instead.
            var turnContext = await DeserializeTurnContext("message.shared_channel.wrong_team_id.json");
            var env = TestEnvironment.Create<TestData>();

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.False(message.Payload.Ignore); // Abbot not mentioned.
            Assert.Equal("T00012121", message.Organization.PlatformId);
            Assert.Equal("U000000F11", message.From.User.PlatformUserId);
            Assert.Equal("T000998899", message.From.Organization.PlatformId);
            Assert.NotNull(message.Room);
            Assert.Equal("C00000002", message.Room.PlatformRoomId);
            Assert.Equal("support-room", message.Room.Name);
        }

        [Fact]
        public async Task CreatesMessageFromSharedChannel()
        {
            var turnContext = await DeserializeTurnContext("message.shared_channel.json");
            var env = TestEnvironment.Create<TestData>();

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.False(message.Payload.Ignore); // Abbot not mentioned.
            Assert.Equal("T00012121", message.Organization.PlatformId);
            Assert.Equal("U000000F11", message.From.User.PlatformUserId);
            Assert.Equal("T000998899", message.From.Organization.PlatformId);
            Assert.NotNull(message.Room);
            Assert.Equal("C00000002", message.Room.PlatformRoomId);
            Assert.Equal("support-room", message.Room.Name);
        }

        [Fact]
        public async Task CreatesAppMentionMessageFromRealJson()
        {
            var turnContext = await DeserializeTurnContext<IEventEnvelope<AppMentionEvent>>("app_mention.json");
            var env = TestEnvironment.Create<TestData>();

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.False(message.Payload.Ignore); // Abbot not mentioned.
            Assert.Equal("T00012121", message.Organization.PlatformId);
            Assert.Equal("U00000042", message.From.User.PlatformUserId);
            Assert.NotNull(message.Room);
            Assert.Equal("C00000001", message.Room.PlatformRoomId);
            Assert.Equal("general", message.Room.Name);
            Assert.NotNull(message.ThreadId);
            Assert.Equal("1642968832.004200", message.ThreadId);
            Assert.NotNull(message.ReplyInThreadMessageTarget);
            Assert.Equal("1642968832.004200", message.ReplyInThreadMessageTarget.Address.ThreadId);
        }

        [Fact]
        public async Task CreateMessageActionFromRealJson()
        {
            var turnContext = await DeserializeTurnContext<MessageActionPayload>("message_action.json");
            var env = TestEnvironment.Create<TestData>();

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
        }

        [Fact]
        public async Task CreateMessageActionFromRealJsonWhenAppIdMatchesOrganizationIntegrationAppId()
        {
            var turnContext = await DeserializeTurnContext<MessageActionPayload>("message_action.json");
            var env = TestEnvironment.Create<TestData>();
            var slackIntegration = await env.Integrations.EnableAsync(
                env.TestData.Organization,
                IntegrationType.SlackApp,
                env.TestData.Member);
            slackIntegration.ExternalId = env.TestData.Organization.BotAppId;
            var newSettings = env.Integrations.ReadSettings<SlackAppSettings>(slackIntegration) with
            {
                Authorization = new(env.TestData.Organization),
            };
            await env.Integrations.SaveSettingsAsync(slackIntegration, newSettings);
            turnContext.SetIntegrationId(slackIntegration.Id);
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
        }

        [Fact]
        public async Task ReturnsRoomMembershipEventWhenBotRemovedFromRoom()
        {
            var turnContext = await DeserializeTurnContext("message.removed-from-channel.json", type: "event");
            var env = TestEnvironment.Create<TestData>();
            var organization = await env.CreateOrganizationAsync(platformId: "T013108BYLS", botUserId: "U041D3B6JJY");
            var currentAbbot = await env.CreateMemberAsync(platformUserId: "U041D3B6JJY", org: organization);
            currentAbbot.User.IsAbbot = true;
            await env.Db.SaveChangesAsync();
            await env.CreateRoomAsync(platformRoomId: "C00001212", name: "haacked-dev-test-2", org: organization);
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateEventAsync(turnContext);

            var payloadEvent = Assert.IsAssignableFrom<IPlatformEvent<RoomMembershipEventPayload>>(message);
            var payload = payloadEvent.Payload;
            Assert.NotNull(message);
            Assert.Equal(organization.Id, message.Organization.Id);
            Assert.Equal(MembershipChangeType.Removed, payload.Type);
            Assert.Equal("C00001212", payload.PlatformRoomId);
            Assert.Equal("U041D3B6JJY", payload.PlatformUserId);
            Assert.True(message.From.IsAbbot());
            Assert.Equal(organization.PlatformBotUserId, message.From.User.PlatformUserId);
        }

        [Fact]
        public async Task ReturnsRoomMembershipEventWhenOtherBotRemovedFromRoom()
        {
            var turnContext = await DeserializeTurnContext("message.removed-from-channel.json", type: "event");
            var env = TestEnvironment.Create<TestData>();
            var organization = await env.CreateOrganizationAsync(platformId: "T013108BYLS", botUserId: "U01TG976JSW");
            await env.CreateRoomAsync(platformRoomId: "C00001212", name: "haacked-dev-test-2", org: organization);
            var oldAbbot = await env.CreateMemberAsync(platformUserId: "U041D3B6JJY", org: organization);
            oldAbbot.User.IsAbbot = true;
            await env.Db.SaveChangesAsync();
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateEventAsync(turnContext);

            var payloadEvent = Assert.IsAssignableFrom<IPlatformEvent<RoomMembershipEventPayload>>(message);
            var payload = payloadEvent.Payload;
            Assert.NotNull(message);
            Assert.Equal(MembershipChangeType.Removed, payload.Type);
            Assert.Equal("C00001212", payload.PlatformRoomId);
            Assert.Equal("U041D3B6JJY", payload.PlatformUserId);
            Assert.True(message.From.IsAbbot());
            Assert.NotEqual("abbot", message.From.User.PlatformUserId);
            Assert.NotEqual(organization.PlatformBotUserId, message.From.User.PlatformUserId);
        }

        [Fact]
        public async Task ReturnsNullWhenBotRemovedFromRoomWithUnknownName()
        {
            var turnContext = await DeserializeTurnContext("message.removed-from-channel.json");
            var env = TestEnvironment.Create<TestData>();
            var organization = await env.CreateOrganizationAsync(platformId: "T013108BYLS", botUserId: "U041D3B6JJY");
            await env.CreateRoomAsync(platformRoomId: "C00001212", name: "not-haacked-dev-test-2", org: organization);
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateEventAsync(turnContext);

            Assert.Null(message);
        }

        [Fact]
        public async Task ReturnsNullWhenIntegrationAndAppIdMatchesButNotForThisOrg()
        {
            var turnContext = await DeserializeTurnContext<MessageActionPayload>("message_action.json", type: "event");
            var env = TestEnvironment.Create<TestData>();
            await env.Integrations.EnableAsync(
                env.TestData.Organization,
                IntegrationType.SlackApp,
                env.TestData.Member);
            var slackIntegration = await env.Integrations.EnableAsync(
                env.TestData.ForeignOrganization,
                IntegrationType.SlackApp,
                env.TestData.Member);
            slackIntegration.ExternalId = env.TestData.Organization.BotAppId;
            var newSettings = env.Integrations.ReadSettings<SlackAppSettings>(slackIntegration);
            await env.Integrations.SaveSettingsAsync(slackIntegration, newSettings);

            turnContext.SetIntegrationId(slackIntegration.Id);
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.Null(message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("custom-app")]
        public async Task ReturnsNullWhenIntegrationMatchesButAppIdDoesNot(string integrationAppId)
        {
            var turnContext = await DeserializeTurnContext<MessageActionPayload>("message_action.json");
            var env = TestEnvironment.Create<TestData>();
            var slackIntegration = await env.Integrations.EnableAsync(
                env.TestData.Organization,
                IntegrationType.SlackApp,
                env.TestData.Member);
            slackIntegration.ExternalId = integrationAppId;
            var newSettings = env.Integrations.ReadSettings<SlackAppSettings>(slackIntegration);
            await env.Integrations.SaveSettingsAsync(slackIntegration, newSettings);
            turnContext.SetIntegrationId(slackIntegration.Id);
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.Null(message);
        }

        [Fact]
        public async Task ReturnsMessageWhenIntegrationNotSpecifiedWhileEnabledButAppIdDoesNotMatch()
        {
            var turnContext = await DeserializeTurnContext<MessageActionPayload>("message_action.json");
            var env = TestEnvironment.Create<TestData>();
            await env.Integrations.EnableAsync(
                env.TestData.Organization,
                IntegrationType.SlackApp,
                env.TestData.Member);
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Same(env.TestData.Organization, message.Organization);
        }

        [Fact]
        public async Task ReturnsMessageWhenIntegrationNotSpecifiedWhileAppIdMatchesButNotEnabled()
        {
            var turnContext = await DeserializeTurnContext<MessageActionPayload>("message_action.json");
            var env = TestEnvironment.Create<TestData>();
            var slackIntegration = await env.Integrations.EnableAsync(
                env.TestData.Organization,
                IntegrationType.SlackApp,
                env.TestData.Member);
            await env.Integrations.DisableAsync(env.TestData.Organization,
                IntegrationType.SlackApp,
                env.TestData.Member);
            slackIntegration.ExternalId = env.TestData.Organization.BotAppId.Require();
            var slackAppSettings = new SlackAppSettings
            {
                DefaultAuthorization = new(env.TestData.Organization),
            };
            await env.Integrations.SaveSettingsAsync(slackIntegration, slackAppSettings);
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Same(env.TestData.Organization, message.Organization);
        }

        [Fact]
        public async Task ReturnsNullWhenIntegrationNotSpecifiedWhileAppIdMatchesAndEnabled()
        {
            var turnContext = await DeserializeTurnContext<MessageActionPayload>("message_action.json");
            var env = TestEnvironment.Create<TestData>();
            var slackIntegration = await env.Integrations.EnableAsync(
                env.TestData.Organization,
                IntegrationType.SlackApp,
                env.TestData.Member);
            slackIntegration.ExternalId = env.TestData.Organization.BotAppId.Require();
            var slackAppSettings = new SlackAppSettings();
            await env.Integrations.SaveSettingsAsync(slackIntegration, slackAppSettings);
            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.Null(message);
        }

        [Theory]
        // No message ID
        [InlineData(null, null, null, null)]
        [InlineData("", "", null, null)]
        // Not in a thread, so creates a new "thread" for the current message.
        [InlineData("1678236563.400000", null, null, "1678236563.400000")]
        // Already in a thread, so ignores the current message ID.
        [InlineData("1678236563.867530", "1678236563.400000", "1678236563.400000", "1678236563.400000")]
        public async Task CreatesThreadIdCorrectly(
            string messageTs,
            string? threadTs,
            string expectedThreadId,
            string expectedReplyThreadId)
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        team_id = "T00012121",
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "message",
                            team = "T00012121",
                            user = env.TestData.User.PlatformUserId,
                            ts = messageTs,
                            thread_ts = threadTs,
                            channel = "C00000001",
                            blocks = new object[] { }
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Equal(expectedThreadId, message.ThreadId);
            Assert.Equal(expectedReplyThreadId, message.ReplyInThreadMessageTarget?.Address.ThreadId);
        }

        [Theory]
        [InlineData(null, null, "1678236563.517809")]
        [InlineData("1678236563.400000", "1678236563.400000", "1678236563.400000")]
        public async Task CreatesRoomAndThreadCorrectly(
            string? threadTimestamp,
            string? expectedThreadId,
            string expectedReplyThreadId)
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        team_id = "T00012121",
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "message",
                            team = "T00012121",
                            user = env.TestData.User.PlatformUserId,
                            channel = "C00000001",
                            ts = "1678236563.517809",
                            thread_ts = threadTimestamp,
                            blocks = new object[] { }
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.NotNull(message.Room);
            Assert.Equal("C00000001", message.Room.PlatformRoomId);
            Assert.Equal(expectedThreadId, message.ThreadId);
            Assert.Equal(expectedReplyThreadId, message.ReplyInThreadMessageTarget?.Address.ThreadId);
        }

        [Fact]
        public async Task FallsBackToEventTeamIdIfUserTeamIdNull()
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        team_id = organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "message",
                            user = env.TestData.User.PlatformUserId,
                            ts = "42",
                            channel = "C00000001",
                            blocks = new object[] { }
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Equal(organization.PlatformId, message.From.Organization.PlatformId);
        }

        [Fact]
        public async Task CreatesSlackPlatformMessageFromDirectSlackMessageEvent()
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var turnContext = FakeTurnContextFactory.CreateTurnContext();
            turnContext.Activity.ChannelData = new EventEnvelope<MessageEvent>
            {
                ApiAppId = organization.BotAppId!,
                TeamId = organization.PlatformId,
                Event = new MessageEvent
                {
                    User = user.PlatformUserId,
                    Timestamp = "1234567890",
                    Team = organization.PlatformId,
                    Channel = "C00000001"
                }
            };

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Equal("1234567890", message.Payload.MessageId);
            Assert.Equal(user.PlatformUserId, message.From.User.PlatformUserId);
            Assert.Equal(organization.PlatformBotId, message.Bot.Id);
            var slackBot = Assert.IsType<SlackBotChannelUser>(message.Bot);
            Assert.Equal(organization.PlatformBotUserId, slackBot.UserId);
            Assert.Equal("TheAbbot", message.Bot.DisplayName);
            Assert.NotNull(message.Room);
            Assert.Equal("C00000001", message.Room.PlatformRoomId);
            Assert.Equal("general", message.Room.Name);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task CreatesSlackPlatformMessageWithMentions(bool isChannel, bool isAttachable)
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        team_id = organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "app_mention",
                            user = env.TestData.User.PlatformUserId,
                            team = organization.PlatformId,
                            ts = "1640027021.060900",
                            channel = "C00000001",
                            blocks = new object[]
                            {
                                new
                                {
                                    type = "rich_text",
                                    block_id = "vRKu",
                                    elements = new object[]
                                    {
                                        new
                                        {
                                            type = "rich_text_section",
                                            elements = new object[]
                                            {
                                                new
                                                {
                                                    type = "text",
                                                    text = "Hello"
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = organization.PlatformBotUserId
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = "U00000001"
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = "U00000002"
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.False(message.Payload.Ignore); // Never ignore app_mention
            Assert.Equal("1640027021.060900", message.Payload.MessageId);
            Assert.NotNull(message.Organization.ApiToken);
            Assert.Equal("xoxb-this-is-a-test-token", message.Organization.ApiToken?.Reveal());
            Assert.Equal(env.TestData.User.PlatformUserId, message.From.User.PlatformUserId);
            Assert.Equal(organization.PlatformBotId, message.Bot.Id);
            var slackBot = Assert.IsType<SlackBotChannelUser>(message.Bot);
            Assert.Equal(organization.PlatformBotUserId, slackBot.UserId);
            Assert.Equal(organization.BotName, message.Bot.DisplayName);
            Assert.Equal(2, message.Mentions.Count);
            Assert.Equal("U00000001", message.Mentions[0].User.PlatformUserId);
            Assert.Equal("U00000002", message.Mentions[1].User.PlatformUserId);
            Assert.Equal("Bojack", message.Mentions[0].DisplayName);
            Assert.Equal("Diane", message.Mentions[1].DisplayName);
            Assert.NotNull(message.Room);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task CreatesSlackPlatformMessageWithPersistentRoomSetCorrectly(bool isChannel, bool isAttachable)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.SlackApi.Conversations.AddConversationInfoResponse(organization.ApiToken!.Reveal(),
                new ConversationInfo
                {
                    Id = "C00000099",
                    IsChannel = isChannel,
                    IsInstantMessage = !isChannel,
                    Name = "the-room"
                });

            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        team_id = organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "app_mention",
                            user = env.TestData.User.PlatformUserId,
                            team = organization.PlatformId,
                            ts = "1640027021.060900",
                            channel = "C00000099",
                            blocks = new object[] { }
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message?.Room);
            Assert.Equal(isAttachable, message.Room.Persistent);
            Assert.False(message.DirectMessage);
        }

        [Fact]
        public async Task CreatesSlackPayloadMessage()
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    Payload = new {
                        trigger_id = "trigger-123",
                        team = new {
                            id = organization.PlatformId
                        },
                        user = new {
                            id = env.TestData.User.PlatformUserId
                        },
                        channel = new {
                            id = "C00000001",
                            name = "general",
                        },
                        type = "interactive_message",
                        message_ts = "1641665238.009700",
                        callback_id = "s:42:",
                        original_message = new {
                            app_id = organization.BotAppId,
                            text = "Click a button",
                            ts = "1641665238.009700"
                        },
                        actions = new[]
                        {
                            new
                            {
                                name = "green",
                                type = "button",
                                value = "foo bar"
                            }
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Equal(organization.PlatformId, message.Organization.PlatformId);
            Assert.Equal("1641665238.009700", message.Payload.MessageId);
            Assert.Equal(organization.ApiToken, message.Organization.ApiToken);
            Assert.Equal(env.TestData.User.PlatformUserId, message.From.User.PlatformUserId);
            Assert.Equal(organization.PlatformBotId, message.Bot.Id);
            Assert.IsType<SlackBotChannelUser>(message.Bot);
            Assert.Equal(organization.BotName, message.Bot.DisplayName);
            Assert.Empty(message.Mentions);
            Assert.False(message.DirectMessage);
            Assert.False(message.Payload.Ignore);
            Assert.NotNull(message.Payload);
            var payload = Assert.IsType<MessageEventInfo>(message.Payload);
            Assert.NotNull(payload.InteractionInfo);
            Assert.Equal("trigger-123", payload.InteractionInfo.TriggerId);
            var userSkillCallbackInfo = Assert.IsType<UserSkillCallbackInfo>(payload.InteractionInfo.CallbackInfo);
            Assert.Equal(42, userSkillCallbackInfo.SkillId);
            Assert.Equal("foo bar", payload.InteractionInfo.Arguments);
        }

        [Fact]
        public async Task CreatesPlatformMessageFromBotWorkflowMessage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync();
            var foreignUser = env.TestData.ForeignUser;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                text:
                $"*Haack Support* submission from \u003c@{foreignUser.PlatformUserId}\u003e *Whatddya want?! * I need help.",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        api_app_id = organization.BotAppId,
                        event_id = "Ev000000006",
                        event_time = 1658946302,
                        is_ext_shared_channel = true,
                        team_id = teamId,
                        @event = new {
                            type = "message",
                            subtype = "bot_message",
                            text =
                                $"*Haack Support* submission from \u003c@{foreignUser.PlatformUserId}\u003e *Whatddya want?! * I need help.",
                            ts = "1659137461.985879",
                            bot_id = "B03R6C0AMU5",
                            app_id = "A01234545",
                            team = teamId,
                            bot_profile = new {
                                id = "B03R6C0AMU5",
                                deleted = false,
                                name = "Some Workflow",
                                updated = 1658945797,
                                app_id = "A01234545",
                                icons = new {
                                    image_36 = "https://example.com/gif.gif",
                                    image_48 = "https://example.com/gif.gif",
                                    image_72 = "https://example.com/gif.gif"
                                },
                                team_id = "T013108BYLS",
                                is_workflow_bot = true,
                            },
                            blocks = new object[]
                            {
                                new
                                {
                                    type = "section",
                                    block_id = "n0I",
                                    text = new
                                    {
                                        type = "mrkdwn",
                                        text =
                                            $"*Haack Support* submission from \u003c@{foreignUser.PlatformUserId}\u003e",
                                        verbatim = false
                                    },
                                },
                                new
                                {
                                    type = "section",
                                    block_id = "V706",
                                    text = new
                                    {
                                        type = "mrkdwn",
                                        text = "*Whatddya want?! *\nI need help.\n\n",
                                        verbatim = false
                                    },
                                },
                                new
                                {
                                    type = "divider",
                                    block_id = "acU"
                                }
                            },
                            channel = room.PlatformRoomId,
                            event_ts = "1501234567.123456",
                            channel_type = "channel"
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            var payload = message.Payload;
            Assert.NotNull(payload);
            Assert.Equal(
                $"*Haack Support* submission from \u003c@{foreignUser.PlatformUserId}\u003e *Whatddya want?! * I need help.",
                message.Text);

            Assert.Equal(foreignUser.Id, message.From.UserId);
            Assert.True(message.Payload.WorkflowMessage);
        }

        [Theory]
        [InlineData("b:echo", "echo", null)]
        [InlineData("b:echo:", "echo", "")]
        [InlineData("b:echo:context-id", "echo", "context-id")]
        public async Task WithBuiltInSkillCreatesSlackBlockActionsMessage(
            string blockId,
            string? expectedSkill,
            string? expectedContextId)
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    Payload = new {
                        type = "block_actions",
                        trigger_id = "trigger-123",
                        api_app_id = organization.BotAppId,
                        user = new {
                            id = env.TestData.User.PlatformUserId,
                            team_id = organization.PlatformId
                        },
                        container = new {
                            type = "message",
                            message_ts = "1641665238.009700",
                            channel_id = "C00000001",
                        },
                        team = new {
                            id = organization.PlatformId,
                            domain = organization.Domain,
                        },
                        channel = new {
                            id = "C00000001",
                            name = "general",
                        },
                        message = new {
                            type = "message",
                            subtype = "bot_message",
                            text = "Click a button",
                            ts = "1641665238.009700",
                            blocks = new[]
                            {
                                new
                                {
                                    type = "actions",
                                    block_id = blockId,
                                    elements = new[]
                                    {
                                        new
                                        {
                                            type = "button",
                                            action_id = "confirm_button",
                                            text = new
                                            {
                                                type = "plain_text",
                                                text = "Yes"
                                            },
                                            value = "foo bar"
                                        },
                                    }
                                }
                            }
                        },
                        actions = new[]
                        {
                            new
                            {
                                action_id = "confirm_button",
                                block_id = blockId,
                                type = "button",
                                text = new
                                {
                                    type = "plain_text",
                                    text = "Yes"
                                },
                                value = "foo bar"
                            }
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Equal(organization.PlatformId, message.Organization.PlatformId);
            Assert.Equal("1641665238.009700", message.Payload.MessageId);
            Assert.Same(organization, message.Organization);
            Assert.Equal(env.TestData.User.PlatformUserId, message.From.User.PlatformUserId);
            Assert.Equal(organization.PlatformBotId, message.Bot.Id);
            Assert.IsType<SlackBotChannelUser>(message.Bot);
            Assert.Equal(organization.BotName, message.Bot.DisplayName);
            Assert.Empty(message.Mentions);
            Assert.False(message.DirectMessage);
            Assert.False(message.Payload.Ignore);
            Assert.NotNull(message.Payload);
            var payload = Assert.IsType<MessageEventInfo>(message.Payload);
            Assert.NotNull(payload.InteractionInfo);
            Assert.Equal("trigger-123", message.TriggerId);
            Assert.Equal("trigger-123", payload.InteractionInfo.TriggerId);
            var builtInCallbackInfo = Assert.IsType<BuiltInSkillCallbackInfo>(payload.InteractionInfo.CallbackInfo);
            Assert.Equal(expectedSkill, builtInCallbackInfo.SkillName);
            Assert.Equal(expectedContextId, builtInCallbackInfo.ContextId);
            Assert.Equal("foo bar", payload.InteractionInfo.Arguments);
        }

        [Theory]
        [InlineData("s:42", 42, null)]
        [InlineData("s:42:", 42, "")]
        [InlineData("s:42:context-id", 42, "context-id")]
        public async Task WithUserSkillCreatesSlackBlockActionsMessage(
            string blockId,
            int? expectedSkillId,
            string? expectedContextId)
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    Payload = new {
                        type = "block_actions",
                        api_app_id = organization.BotAppId,
                        user = new {
                            id = env.TestData.User.PlatformUserId,
                            team_id = organization.PlatformId
                        },
                        container = new {
                            type = "message",
                            message_ts = "1641665238.009700",
                            channel_id = "C00000001",
                        },
                        team = new {
                            id = organization.PlatformId,
                            domain = organization.Domain,
                        },
                        channel = new {
                            id = "C00000001",
                            name = "general",
                        },
                        message = new {
                            type = "message",
                            subtype = "bot_message",
                            text = "Click a button",
                            ts = "1641665238.009700",
                            blocks = new[]
                            {
                                new
                                {
                                    type = "actions",
                                    block_id = blockId,
                                    elements = new[]
                                    {
                                        new
                                        {
                                            type = "button",
                                            action_id = "confirm_button",
                                            text = new
                                            {
                                                type = "plain_text",
                                                text = "Yes"
                                            },
                                            value = "foo bar"
                                        },
                                    }
                                }
                            }
                        },
                        actions = new[]
                        {
                            new
                            {
                                action_id = "confirm_button",
                                block_id = blockId,
                                type = "button",
                                text = new
                                {
                                    type = "plain_text",
                                    text = "Yes"
                                },
                                value = "foo bar"
                            }
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Equal(organization.PlatformId, message.Organization.PlatformId);
            Assert.Equal("1641665238.009700", message.Payload.MessageId);
            Assert.Same(organization, message.Organization);
            Assert.Equal(env.TestData.User.PlatformUserId, message.From.User.PlatformUserId);
            Assert.Equal(organization.PlatformBotId, message.Bot.Id);
            Assert.IsType<SlackBotChannelUser>(message.Bot);
            Assert.Equal(organization.BotName, message.Bot.DisplayName);
            Assert.Empty(message.Mentions);
            Assert.False(message.DirectMessage);
            Assert.False(message.Payload.Ignore);
            Assert.NotNull(message.Payload);
            var payload = Assert.IsType<MessageEventInfo>(message.Payload);
            Assert.NotNull(payload.InteractionInfo);
            var userSkillCallbackInfo = Assert.IsType<UserSkillCallbackInfo>(payload.InteractionInfo.CallbackInfo);
            Assert.Equal(expectedSkillId, userSkillCallbackInfo.SkillId);
            Assert.Equal(expectedContextId, userSkillCallbackInfo.ContextId);
            Assert.Equal("foo bar", payload.InteractionInfo.Arguments);
        }

        [Fact]
        public async Task ReturnsNullIfSlackMessageHasEmptyEventBody()
        {
            var env = TestEnvironment.Create<TestData>();
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        @event = new {
                            type = ""
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.Null(message);
        }

        [Fact]
        public async Task SetsPlatformIdForUsersInAnotherSlack()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync(
                "T0000000001",
                apiToken: "xoxb-this-is-a-test-token");

            await env.CreateOrganizationAsync("T0000000002");
            env.SlackApi.AddTeamInfo("xoxb-this-is-a-test-token",
                "T0000000003",
                new TeamInfo
                {
                    Id = "T0000000003",
                    Name = "the-team",
                    Domain = "the-team",
                    Icon = new Icon
                    {
                        Image68 = "https://example.com/icon.png"
                    }
                });

            env.SlackApi.Conversations.AddConversationInfoResponse(
                "xoxb-this-is-a-test-token",
                new ConversationInfo
                {
                    Id = "C000000001",
                    Name = "the-room"
                });

            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U0000000042",
                    TeamId = organization.PlatformId,
                    Name = "Marvin"
                });

            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U00000001",
                    TeamId = "T0000000002",
                    Name = "Bojack"
                });

            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U00000002",
                    TeamId = "T0000000003",
                    Name = "Diane"
                });

            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                text: "`hello`",
                channelData: new {
                    ApiToken = "the-api-token",
                    SlackMessage = new {
                        type = "event_callback",
                        team_id = organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "app_mention",
                            team = "T0000000003",
                            user = "U0000000042",
                            channel = "C000000001",
                            blocks = new object[]
                            {
                                new
                                {
                                    type = "rich_text",
                                    block_id = "vRKu",
                                    elements = new object[]
                                    {
                                        new
                                        {
                                            type = "rich_text_section",
                                            elements = new object[]
                                            {
                                                new
                                                {
                                                    type = "text",
                                                    text = "Hello"
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = organization.PlatformBotUserId
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = "U00000001"
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = "U00000002"
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Equal(organization.PlatformId, message.Organization.PlatformId);
            Assert.Equal("U0000000042", message.From.User.PlatformUserId);
            Assert.Equal("T0000000001", message.From.Organization.PlatformId);
            Assert.Equal(organization.PlatformBotId, message.Bot.Id);
            Assert.Equal("T0000000001", message.Bot.PlatformId);
            var slackBot = Assert.IsType<SlackBotChannelUser>(message.Bot);
            Assert.Equal(organization.PlatformBotUserId, slackBot.UserId);
            Assert.Equal(organization.BotName, message.Bot.DisplayName);
            Assert.Equal(PlatformType.Slack, message.Organization.PlatformType);
            Assert.Equal(2, message.Mentions.Count);
            Assert.Equal("U00000001", message.Mentions[0].User.PlatformUserId);
            Assert.Equal("T0000000002", message.Mentions[0].Organization.PlatformId);
            Assert.Equal("U00000002", message.Mentions[1].User.PlatformUserId);
            Assert.Equal("T0000000003", message.Mentions[1].Organization.PlatformId);
        }

        [Fact]
        public async Task SetsMessagePropertiesAndResponderCorrectly()
        {
            var env = TestEnvironment.Create<TestData>();
            var organization = env.TestData.Organization;
            organization.Scopes = "chat:write.customize";
            organization.BotResponseAvatar = "https://example.com/response-avatar.png";
            await env.Db.SaveChangesAsync();
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                text: "`hello`",
                channelData: new {
                    ApiToken = organization.ApiToken?.Reveal(),
                    SlackMessage = new {
                        type = "event_callback",
                        team_id = organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "app_mention",
                            team = organization.PlatformId,
                            user = env.TestData.User.PlatformUserId,
                            channel = "C00000001",
                            blocks = new object[]
                            {
                                new
                                {
                                    type = "rich_text",
                                    block_id = "vRKu",
                                    elements = new object[]
                                    {
                                        new
                                        {
                                            type = "rich_text_section",
                                            elements = new object[]
                                            {
                                                new
                                                {
                                                    type = "text",
                                                    text = "Hello"
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = organization.PlatformBotUserId
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = "U00000001"
                                                },
                                                new
                                                {
                                                    type = "user",
                                                    user_id = "U00000002"
                                                }
                                            }
                                        }
                                    }
                                },
                                new
                                {
                                    type = "rich_text",
                                    block_id = "vRKu",
                                    elements = new object[]
                                    {
                                        new
                                        {
                                            type = "user",
                                            user_id = organization.PlatformBotUserId
                                        },
                                    }
                                }
                            }
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);
            Assert.Equal(organization.PlatformId, message.Organization.PlatformId);
            Assert.Equal(env.TestData.User.PlatformUserId, message.From.User.PlatformUserId);
            Assert.Equal(organization.PlatformId, message.From.Organization.PlatformId);
            Assert.Equal(organization.PlatformBotId, message.Bot.Id);
            Assert.Equal(organization.PlatformId, message.Bot.PlatformId);
            var slackBot = Assert.IsType<SlackBotChannelUser>(message.Bot);
            Assert.Equal(organization.PlatformBotUserId, slackBot.UserId);
            Assert.Equal(organization.BotName, message.Bot.DisplayName);
            Assert.Same(organization, message.Organization);
            Assert.Equal(PlatformType.Slack, message.Organization.PlatformType);
            Assert.Equal(2, message.Mentions.Count);
            Assert.Equal("U00000001", message.Mentions[0].User.PlatformUserId);
            Assert.Equal("U00000002", message.Mentions[1].User.PlatformUserId);
        }

        [Fact]
        public async Task TranslatesMessageActionPayload()
        {
            var env = TestEnvironment.Create<TestData>();
            var room = await env.CreateRoomAsync();
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                channelData: new {
                    Payload = new MessageActionPayload()
                    {
                        ApiAppId = env.TestData.Organization.BotAppId,
                        Team = new TeamInfo()
                        {
                            Id = env.TestData.Organization.PlatformId
                        },
                        Channel = new ChannelInfo()
                        {
                            Id = room.PlatformRoomId
                        },
                        User = new UserInfo()
                        {
                            Id = env.TestData.User.PlatformUserId
                        },
                        CallbackId = "i:Test",
                        MessageTimestamp = "1111.2222",
                        TriggerId = "trigger-123",
                        Message = new()
                        {
                            ThreadTimestamp = "2222.1111",
                        },
                        ResponseUrl = new Uri("https://example.com/response"),
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var message = await factory.TranslateMessageAsync(turnContext);

            Assert.NotNull(message);

            // Basic IPlatformMessage values
            Assert.Equal(env.TestData.Organization.Id, message.Organization.Id);
            Assert.Equal(env.TestData.User.Id, message.From.Id);
            Assert.Equal(env.TestData.Organization.PlatformBotId, message.Bot.Id);
            Assert.Empty(message.Mentions);
            Assert.NotNull(message.Room);
            Assert.Equal(room.Id, message.Room.Id);
            Assert.NotNull(message.ReplyInThreadMessageTarget);

            // Payload and Interaction Info
            Assert.Equal(string.Empty, message.Payload.Text);
            Assert.Equal(env.TestData.User.PlatformUserId, message.Payload.PlatformUserId);
            Assert.Equal(room.PlatformRoomId, message.Payload.PlatformRoomId);
            Assert.Empty(message.Payload.MentionedUserIds);
            Assert.False(message.Payload.Ignore);
            Assert.False(message.Payload.DirectMessage);
            Assert.Equal("1111.2222", message.Payload.MessageId);
            Assert.Equal("2222.1111", message.Payload.ThreadId);
            Assert.NotNull(message.Payload.InteractionInfo);
            Assert.False(message.Payload.InteractionInfo.Ephemeral);
            Assert.Equal("1111.2222", message.Payload.InteractionInfo.ActivityId);
            Assert.Equal("trigger-123", message.Payload.InteractionInfo.TriggerId);
            Assert.Equal(new InteractionCallbackInfo("Test"), message.Payload.InteractionInfo.CallbackInfo);
            Assert.Equal(string.Empty, message.Payload.InteractionInfo.Arguments);
            Assert.Equal("https://example.com/response", message.Payload.InteractionInfo.ResponseUrl?.ToString());
        }
    }

    public class TheTranslateInstallEventAsyncMethod
    {
        [Fact]
        public async Task QueriesSlackApiAndCreatesSlackPlatformInstallationMessage()
        {
            var env = TestEnvironment.CreateWithoutData();
            env.SlackApi.AddTeamInfo(
                "apiToken",
                "T8675309",
                new TeamInfo
                {
                    Id = "T8675309",
                    Name = "The A Team",
                    Domain = "the-a-team",
                    Icon = new Icon
                    {
                        Image68 = "https://example.com/a-team-icon.png"
                    }
                });

            env.SlackApi.AddTeamInfoHeader(
                "apiToken",
                "T8675309",
                "X-OAuth-Scopes",
                new[] { "app_mentions:read,channels:history,channels:read" });

            env.SlackApi.AddBotsInfo("apiToken",
                "B0123421",
                new BotInfo
                {
                    Id = "id1",
                    AppId = "A01TG9GPJQ3",
                    UserId = "U0123456789",
                    Name = "AbbotApp",
                    Icons = new("img36.png", "img48.png", "https://example.com/icon.png")
                });

            env.SlackApi.AddUserInfoResponse("apiToken",
                new UserInfo
                {
                    Id = "U0123456789",
                    TeamId = "T8675309",
                    Name = "The Real Abbot"
                });

            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "installationUpdate",
                channelData: new {
                    SlackMessage = new {
                        type = "bot_added",
                        bot = new {
                            id = "B0123421",
                            app_id = "A01TG9GPJQ3",
                            name = "the-abbot"
                        }
                    },
                    ApiToken = "apiToken"
                },

                // NOTE: Sender is ignored for bot_added events, we just put the Bot's ID in
                sender: new ChannelAccount
                {
                    Id = "U321:T8675309",
                    Name = "phil"
                },
                recipient: new ChannelAccount
                {
                    Id = "B0123421:T8675309",
                    Name = "abbot"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var installEvent = await factory.TranslateInstallEventAsync(turnContext);

            Assert.Equal(PlatformType.Slack, installEvent.PlatformType);
            Assert.Equal("T8675309", installEvent.PlatformId);
            Assert.Equal("The A Team", installEvent.Name);
            Assert.Equal("B0123421", installEvent.BotId);
            Assert.Equal("U0123456789", installEvent.BotUserId);
            Assert.Equal("A01TG9GPJQ3", installEvent.AppId);
            Assert.Equal("The Real Abbot", installEvent.BotName);
            Assert.Equal("AbbotApp", installEvent.BotAppName);
            Assert.Equal("the-a-team.slack.com", installEvent.Domain);
            Assert.Equal("the-a-team", installEvent.Slug);
            Assert.Equal("app_mentions:read,channels:history,channels:read", installEvent.OAuthScopes);
            Assert.NotNull(installEvent.ApiToken);
            Assert.Equal("apiToken", installEvent.ApiToken.Reveal());
            Assert.Equal("https://example.com/icon.png", installEvent.BotAvatar);
        }
    }

    public class TheTranslateUninstallEventMethod
    {
        [Fact]
        public async Task CreatesSlackPlatformUninstallationMessage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "installationUpdate",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        @event = new {
                            type = "app_uninstalled"
                        },
                        api_app_id = organization.BotAppId,
                        team_id = organization.PlatformId
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var uninstallEvent = await factory.TranslateUninstallEventAsync(turnContext);

            Assert.NotNull(uninstallEvent);
            Assert.Equal(organization.PlatformId, uninstallEvent.Organization.PlatformId);
            Assert.Equal(PlatformType.Slack, uninstallEvent.Organization.PlatformType);
            Assert.Equal(organization.PlatformBotUserId, uninstallEvent.From.User.PlatformUserId);

            var payload = Assert.IsType<UninstallPayload>(uninstallEvent.Payload);
            Assert.Equal(organization.PlatformId, payload.PlatformId);
            Assert.Equal(organization.BotAppId, payload.BotAppId);
        }

        [Fact]
        public async Task ReturnsNullWhenOrganizationDoesNotExist()
        {
            var env = TestEnvironment.CreateWithoutData();
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "installationUpdate",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        @event = new {
                            type = "app_uninstalled"
                        },
                        api_app_id = "A987654",
                        team_id = "T001213"
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var uninstallEvent = await factory.TranslateUninstallEventAsync(turnContext);

            Assert.Null(uninstallEvent);
        }
    }

    public class TheTranslateEventAsyncMethod
    {
        [Fact]
        public async Task CreatesPlatformEventFromSlackUserChangeEvent()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = teamId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "user_change",
                            user = new {
                                id = "U6A0HFBS4",
                                team_id = teamId,
                                name = "dice",
                                profile = new {
                                    display_name = "the dice"
                                }
                            }
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            var userEvent = Assert.IsAssignableFrom<IPlatformEvent<UserEventPayload>>(platformEvent);
            Assert.Equal("the dice", userEvent.Payload.DisplayName);
        }

        [Fact]
        public async Task CreatesPlatformEventForCorrectOrganizationBasedOnAppId()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var slackAppIntegration = await env.Integrations.EnableAsync(
                organization,
                IntegrationType.SlackApp,
                env.TestData.Abbot);
            slackAppIntegration.ExternalId = "A00000042";
            await env.Integrations.SaveSettingsAsync(slackAppIntegration, new SlackAppSettings());
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = env.TestData.ForeignOrganization.PlatformId,
                        api_app_id = "A00000042",
                        @event = new {
                            type = "user_change",
                            user = new {
                                id = "U6A0HFBS4",
                                team_id = "WHATEVS",
                                name = "dice",
                                profile = new {
                                    display_name = "the dice"
                                }
                            }
                        },
                        authorizations = new[]
                        {
                            new
                            {
                                team_id = "WHATEVS"}
                        },
                    },
                    ApiToken = "apiToken"
                });
            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.NotNull(platformEvent);
            Assert.Equal(organization.Id, platformEvent.Organization.Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("NOT_FOUND")]
        public async Task CreatesPlatformEventForCorrectOrganizationBasedOnAuthorizations(string apiAppId)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = env.TestData.ForeignOrganization.PlatformId,
                        api_app_id = apiAppId,
                        @event = new {
                            type = "user_change",
                            user = new {
                                id = "U6A0HFBS4",
                                team_id = teamId,
                                name = "dice",
                                profile = new {
                                    display_name = "the dice"
                                }
                            }
                        },
                        authorizations = new[]
                        {
                            new
                            {
                                team_id = teamId,
                            }
                        },
                    },
                    ApiToken = "apiToken"
                });
            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.NotNull(platformEvent);
            Assert.Equal(organization.Id, platformEvent.Organization.Id);
        }

        [Fact]
        public async Task CreatesPlatformEventFromAppHomeOpenedEvent()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = teamId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "app_home_opened",
                            user = user.PlatformUserId,
                            channel = "C6A0HFBS4",
                            tab = "home",
                            event_ts = "1501234567.123456"
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            var appHomeEvent = Assert.IsAssignableFrom<IPlatformEvent<AppHomeOpenedEvent>>(platformEvent);
            var payload = appHomeEvent.Payload;
            Assert.Equal("app_home_opened", payload.Type);
            Assert.Equal("home", payload.Tab);
            Assert.Equal(user.PlatformUserId, appHomeEvent.From.User.PlatformUserId);
        }

        [Fact]
        public async Task CreatesPlatformEventFromMessageDeletedEvent()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var user = env.TestData.User;
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = teamId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "message",
                            subtype = "message_deleted",
                            channel = room.PlatformRoomId,
                            deleted_ts = "1501234560.00006",
                            event_ts = "1501234567.123456",
                            previous_message = new {
                                user = user.PlatformUserId,
                            }
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            var messageDeletedEvent = Assert.IsAssignableFrom<IPlatformEvent<MessageDeletedEvent>>(platformEvent);
            var payload = messageDeletedEvent.Payload;
            Assert.Equal("message", payload.Type);
            Assert.Equal(user.PlatformUserId, messageDeletedEvent.From.User.PlatformUserId);
        }

        [Fact]
        public async Task CreatesPlatformEventFromMessageChangedEventForDeletedMessage()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var user = env.TestData.ForeignUser;
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var foreignTeamId = env.TestData.ForeignOrganization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = teamId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "message",
                            subtype = "message_changed",
                            channel = room.PlatformRoomId,
                            deleted_ts = "1501234560.00006",
                            event_ts = "1501234567.123456",
                            message = new {
                                user = user.PlatformUserId,
                                team = foreignTeamId,
                                hidden = true,
                                subtype = "tombstone",
                            },
                            previous_message = new {
                                user = user.PlatformUserId,
                                team = foreignTeamId,
                                hidden = true,
                                subtype = "tombstone",
                            }
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            var messageDeletedEvent = Assert.IsAssignableFrom<IPlatformEvent<MessageChangedEvent>>(platformEvent);
            var payload = messageDeletedEvent.Payload;
            Assert.Equal("message", payload.Type);
            Assert.Equal("message_changed", payload.SubType);
            Assert.Equal("tombstone", payload.Message.SubType);
            Assert.Equal(user.PlatformUserId, messageDeletedEvent.From.User.PlatformUserId);
        }

        [Fact]
        public async Task CreatesPlatformEventFromChannelNameChangedMessageEvent()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = teamId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "message",
                            subtype = "channel_name",
                            channel = room.PlatformRoomId,
                            deleted_ts = "1501234560.00006",
                            event_ts = "1501234567.123456",
                            old_name = "bill",
                            name = "pill"
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            var messageDeletedEvent = Assert.IsAssignableFrom<IPlatformEvent<RoomEventPayload>>(platformEvent);
            var payload = messageDeletedEvent.Payload;
            Assert.Equal(room.PlatformRoomId, payload.PlatformRoomId);
        }

        [Fact]
        public async Task CreatesPlatformEventFromViewBlockActionsPayload()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    Payload = new {
                        type = "block_actions",
                        token = "TOKEN123",
                        view = new {
                            type = "modal",
                            id = "V02134",
                            team_id = teamId
                        },
                        container = new {
                            view_id = "V02134"
                        },
                        team = new {
                            id = teamId
                        },
                        user = new {
                            id = user.PlatformUserId,
                        },
                        api_app_id = organization.BotAppId,
                        trigger_id = "12345.12345.12345"
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            var viewEvent = Assert.IsAssignableFrom<IPlatformEvent<IViewBlockActionsPayload>>(platformEvent);
            var payload = viewEvent.Payload;
            Assert.Equal("block_actions", payload.Type);
            Assert.Equal("V02134", payload.View.Id);
            Assert.Equal("V02134", payload.Container.ViewId);
            Assert.Equal(user.PlatformUserId, viewEvent.From.User.PlatformUserId);
        }

        [Fact]
        public async Task CreatesPlatformEventFromReactionAddedEvent()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var user = env.TestData.User;
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = teamId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "reaction_added",
                            user = user.PlatformUserId,
                            item = new {
                                type = "message",
                                channel = room.PlatformRoomId,
                                ts = "1501234567.123456",
                            },
                            reaction = "eyes",
                            event_ts = "1501234567.123456",
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            var reactionEvent = Assert.IsType<PlatformEvent<ReactionAddedEvent>>(platformEvent);
            var payload = reactionEvent.Payload;
            Assert.Equal("reaction_added", payload.Type);
            Assert.Equal(user.PlatformUserId, reactionEvent.From.User.PlatformUserId);
            Assert.Equal("eyes", reactionEvent.Payload.Reaction);
            Assert.Equal("1501234567.123456", reactionEvent.Payload.Item.Timestamp);
            Assert.NotNull(reactionEvent.Room);
            Assert.Equal(room.Id, reactionEvent.Room.Id);
        }

        [Fact]
        public async Task CreatesPlatformEventFromUnknownEvent()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamId = organization.PlatformId;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = teamId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "unknown",
                            user = "U6A0HFBS4",
                            event_ts = "1360782804.083113"
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.IsAssignableFrom<IPlatformEvent<EventBody>>(platformEvent);
        }

        [Fact]
        public async Task TranslatesChannelRenameEvent()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = env.TestData.Organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "channel_rename",
                            channel = new {
                                id = "C123",
                                name = "channel-name"
                            }
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.NotNull(platformEvent);
            var payload = Assert.IsType<RoomEventPayload>(platformEvent.Payload);
            Assert.Equal("C123", payload.PlatformRoomId);
        }

        [Theory]
        [InlineData("member_joined_channel", MembershipChangeType.Added)]
        [InlineData("member_left_channel", MembershipChangeType.Removed)]
        public async Task TranslatesMemberJoinedOrLeftChannelEvent(string eventType, MembershipChangeType changeType)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = env.TestData.Organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = eventType,
                            channel = "C123",
                            user = "U123",
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.NotNull(platformEvent);
            var payload = Assert.IsType<RoomMembershipEventPayload>(platformEvent.Payload);
            Assert.Equal(changeType, payload.Type);
            Assert.Equal("C123", payload.PlatformRoomId);
            Assert.Equal("U123", payload.PlatformUserId);
        }

        [Fact]
        public async Task TranslatesChannelLeftEvent()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = env.TestData.Organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "channel_left",
                            channel = "C123",
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.NotNull(platformEvent);
            var payload = Assert.IsType<RoomMembershipEventPayload>(platformEvent.Payload);
            Assert.Equal(MembershipChangeType.Removed, payload.Type);
            Assert.Equal("C123", payload.PlatformRoomId);
            Assert.Equal(env.TestData.Organization.PlatformBotUserId, payload.PlatformUserId);
        }

        [Theory]
        [InlineData("channel_deleted")]
        [InlineData("channel_archive")]
        [InlineData("channel_unarchive")]
        [InlineData("group_left")]
        [InlineData("group_deleted")]
        [InlineData("group_archive")]
        [InlineData("group_unarchive")]
        public async Task TranslatesChannelLifecycleEvents(string eventType)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = env.TestData.Organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = eventType,
                            channel = "C123",
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.NotNull(platformEvent);
            var payload = Assert.IsType<RoomEventPayload>(platformEvent.Payload);
            Assert.Equal("C123", payload.PlatformRoomId);
        }

        [Theory]
        [InlineData("channel_convert_to_private")]
        public async Task TranslatesRoomUpdateMessageEvents(string subtype)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = env.TestData.Organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "message",
                            channel = "C123",
                            subtype,
                        }
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.NotNull(platformEvent);
            var payload = Assert.IsType<RoomEventPayload>(platformEvent.Payload);
            Assert.Equal("C123", payload.PlatformRoomId);
        }

        [Fact]
        public async Task ReturnsNullForBotMessageEvents()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "message",
                channelData: new {
                    SlackMessage = new {
                        type = "event_callback",
                        token = "TOKEN123",
                        team_id = env.TestData.Organization.PlatformId,
                        api_app_id = organization.BotAppId,
                        @event = new {
                            type = "message",
                            subtype = "bot_message",
                            user = "U6A0HFBS4",
                            event_ts = "1360782804.083113"
                        }
                    },
                    ApiToken = "apiToken"
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.Null(platformEvent);
        }

        [Fact]
        public async Task CorrectlyTranslatesViewSubmissionFromForeignOrgMember()
        {
            var env = TestEnvironment.Create();
            var turnContext = FakeTurnContextFactory.CreateTurnContext(
                "event",
                channelData: new {
                    Payload = new ViewSubmissionPayload()
                    {
                        Team = new()
                        {
                            Id = env.TestData.ForeignOrganization.PlatformId,
                        },
                        User = new()
                        {
                            Id = env.TestData.ForeignMember.User.PlatformUserId,
                            TeamId = env.TestData.ForeignOrganization.PlatformId,
                        },
                        View = new ModalView()
                        {
                            Title = new PlainText("A Modal"),
                            AppInstalledTeamId = env.TestData.Organization.PlatformId,
                        },
                    }
                });

            var factory = env.Activate<TurnContextTranslator>();

            var platformEvent = await factory.TranslateEventAsync(turnContext);

            Assert.NotNull(platformEvent);
            Assert.Same(env.TestData.Organization, platformEvent.Organization);
            Assert.Same(env.TestData.ForeignMember, platformEvent.From);
        }
    }

    static async Task<ITurnContext<IMessageActivity>> DeserializeTurnContext(
        string channelDataEmbeddedResource,
        string type = "message") =>
        await DeserializeTurnContext<IEventEnvelope<MessageEvent>>(channelDataEmbeddedResource, type);

    static async Task<ITurnContext<IMessageActivity>> DeserializeTurnContext<TPayload>(
        string channelDataEmbeddedResource,
        string type = "message")
    {
        var messageJson = await EmbeddedResourceHelper.ReadSlackChannelDataResource(channelDataEmbeddedResource);
        var slackMessageObject = JsonConvert.DeserializeObject<TPayload>(messageJson);
        return FakeTurnContextFactory.CreateTurnContext(
            type,
            channelData: new {
                SlackMessage = slackMessageObject
            });
    }
}
