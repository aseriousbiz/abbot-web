using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Security;
using Serious.BlockKit.LayoutBlocks;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Xunit;

public class ManageConversationHandlerTests
{
    static SettingsTask Verify(TestEnvironmentWithData env, Conversation conversation) =>
        Verifier.Verify(BuildTarget(env, conversation.Organization, conversation));

    static SettingsTask Verify(TestEnvironmentWithData env, Organization organization, Conversation? conversation = null) =>
        Verifier.Verify(BuildTarget(env, organization, conversation));

    static async Task<object> BuildTarget(TestEnvironmentWithData env, Organization organization, Conversation? conversation = null)
    {
        Assert.True(env.ConfiguredForSnapshot);
        await env.ReloadAsync(conversation);
        return new {
            Conversation = conversation,
            env.Responder.OpenModals,
            env.Responder.SentMessages,
            env.AnalyticsClient,
            AuditEvents = await env.AuditLog.GetRecentActivityAsync(organization),
            Logs = env.GetAllLogs(),
        };
    }

    const string TestZendeskApiUrl = "https://subdomain.zendesk.com/api/v2/tickets/42.json";
    const string TestZendeskWebUrl = "https://subdomain.zendesk.com/agent/tickets/42";
    const string TestHubSpotWebUrl = "https://app.hubspot.com/contacts/22596177/ticket/99999";

    [UsesVerify]
    public class TheOnInteractionAsyncMethod
    {
        [Fact]
        public async Task TracksConversationIfTrackConversationClicked()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var messageId = env.IdGenerator.GetSlackMessageId();
            var platformEvent = env.CreateFakePlatformEvent(new ViewBlockActionsPayload
            {
                TriggerId = "the-trigger",
                Actions = new[]
                {
                    new ButtonElement
                    {
                        ActionId = ManageConversationHandler.ActionIds.TrackConversation,
                        BlockId = ManageConversationHandler.BlockIds.TrackConversation,
                        Value = new ConversationIdentifier(Channel: room.PlatformRoomId, messageId),
                    },
                },
                View = new ModalView
                {
                    Id = "view-id",
                },
            });
            var handler = env.Activate<ManageConversationHandler>();
            var context = new ViewContext<ViewBlockActionsPayload>(platformEvent, handler);

            await handler.OnInteractionAsync(context);

            var convo = Assert.Contains(messageId, env.ConversationTracker.ThreadIdToConversationMappings);
            await Verify(env, convo);
        }

        [Fact]
        public async Task ClosesOpenConversationIfCloseConversationClicked()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var platformEvent = env.CreateFakePlatformEvent(payload: new ViewBlockActionsPayload()
            {
                TriggerId = "the-trigger",
                Actions = new[]
                {
                    new ButtonElement()
                    {
                        ActionId = $"{ConversationState.Closed}",
                        Value = $"{convo.Id}",
                    }
                },
                View = new ModalView
                {
                    Id = "view-id",
                    PrivateMetadata = new AnnouncementHandler.PrivateMetadata(room.PlatformRoomId, convo.FirstMessageId)
                }
            });
            var handler = env.Activate<ManageConversationHandler>();
            var context = new ViewContext<ViewBlockActionsPayload>(platformEvent, handler);

            await handler.OnInteractionAsync(context);

            await Verify(env, convo);
        }

        [Fact]
        public async Task ReopensClosedConversationIfReopenConversationClicked()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Conversations.CloseAsync(convo, env.TestData.Member, env.Clock.UtcNow);
            var platformEvent = env.CreateFakePlatformEvent(payload: new ViewBlockActionsPayload()
            {
                TriggerId = "the-trigger",
                Actions = new[]
                {
                    new ButtonElement()
                    {
                        ActionId = $"{ConversationState.Waiting}",
                        Value = $"{convo.Id}",
                    }
                },
                View = new ModalView
                {
                    Id = "view-id",
                    PrivateMetadata = new AnnouncementHandler.PrivateMetadata(room.PlatformRoomId, convo.FirstMessageId)
                }
            });
            var handler = env.Activate<ManageConversationHandler>();
            var context = new ViewContext<ViewBlockActionsPayload>(platformEvent, handler);

            await handler.OnInteractionAsync(context);

            await Verify(env, convo);
        }

        [Fact]
        public async Task ArchivesConversationIfStopTrackingClicked()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var platformEvent = env.CreateFakePlatformEvent(payload: new ViewBlockActionsPayload()
            {
                TriggerId = "the-trigger",
                Actions = new[]
                {
                    new ButtonElement()
                    {
                        ActionId = $"{ConversationState.Archived}",
                        Value = $"{convo.Id}",
                    }
                },
                View = new ModalView
                {
                    Id = "view-id",
                    PrivateMetadata = new AnnouncementHandler.PrivateMetadata(room.PlatformRoomId, convo.FirstMessageId)
                }
            });
            var handler = env.Activate<ManageConversationHandler>();
            var context = new ViewContext<ViewBlockActionsPayload>(platformEvent, handler);

            await handler.OnInteractionAsync(context);

            await Verify(env, convo);
        }

        [Fact]
        public async Task UnarchivesArchivedConversationIfUnarchiveClicked()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.Conversations.ArchiveAsync(convo, env.TestData.Member, env.Clock.UtcNow);
            var platformEvent = env.CreateFakePlatformEvent(payload: new ViewBlockActionsPayload()
            {
                TriggerId = "the-trigger",
                Actions = new[]
                {
                    new ButtonElement()
                    {
                        ActionId = $"{ConversationState.Closed}",
                        Value = $"{convo.Id}",
                    }
                },
                View = new ModalView
                {
                    Id = "view-id",
                    PrivateMetadata = new AnnouncementHandler.PrivateMetadata(room.PlatformRoomId, convo.FirstMessageId)
                }
            });
            var handler = env.Activate<ManageConversationHandler>();
            var context = new ViewContext<ViewBlockActionsPayload>(platformEvent, handler);

            await handler.OnInteractionAsync(context);

            await Verify(env, convo);
        }

        [Fact]
        public async Task DoesNotOpenModalIfConversationIdIsBogus()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync();
            await env.CreateConversationAsync(room);
            var platformEvent = env.CreateFakePlatformEvent(
                payload: new ViewBlockActionsPayload()
                {
                    TriggerId = "the-trigger",
                    Actions = new[]
                    {
                        new ButtonElement()
                        {
                            ActionId = "link_conversation_action",
                            Value = "1234",
                        }
                    }
                });

            var handler = env.Activate<ManageConversationHandler>();
            var context = new ViewContext<ViewBlockActionsPayload>(platformEvent, handler);

            await handler.OnInteractionAsync(context);

            await Verify(env, room.Organization);
        }
    }

    [UsesVerify]
    public class TheOnMessageInteractionAsyncMethod
    {
        [Fact]
        public async Task RendersMessageWhenPlanCannotManageConversations()
        {
            var env = TestEnvironment.Create(snapshot: true);
            env.TestData.Organization.PlanType = PlanType.Free;

            // Ignored even though enabled
            await env.EnableIntegrationAsync(IntegrationType.Zendesk);
            await env.EnableIntegrationAsync(IntegrationType.HubSpot);
            await env.EnableIntegrationAsync(IntegrationType.GitHub);
            await env.EnableIntegrationAsync(IntegrationType.Ticketing);

            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var platformMessage = env.CreatePlatformMessage(
                room,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            Timestamp = "1111.2222",
                            User = env.TestData.ForeignUser.PlatformUserId // Not the current user's message, thus no announcement option.
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))));

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room.Organization);
        }

        [Theory]
        [InlineData(RoomFlags.Default)] // Bot Is Member; Managed Conversations Disabled
        [InlineData(RoomFlags.BotIsNotMember)]
        [InlineData(RoomFlags.BotIsNotMember | RoomFlags.ManagedConversationsEnabled)]
        public async Task RendersMessageWhenCannotTrackConversation(RoomFlags roomFlags)
        {
            var env = TestEnvironment.Create(snapshot: true);

            // Ignored because disabled
            await env.CreateIntegrationAsync(IntegrationType.Zendesk, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.HubSpot, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.GitHub, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.Ticketing, enabled: false);

            var room = await env.CreateRoomAsync(roomFlags);
            var platformMessage = env.CreatePlatformMessage(
                room,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            Timestamp = "1111.2222",
                            User = env.TestData.ForeignUser.PlatformUserId // Not the current user's message, thus no announcement option.
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))));

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room.Organization)
                .UseParameters(roomFlags);
        }

        [Fact]
        public async Task RendersModalWithMessageWhenInvokedOnMessageNotInAConversation()
        {
            var env = TestEnvironment.Create(snapshot: true);

            // Ignored because disabled
            await env.CreateIntegrationAsync(IntegrationType.Zendesk, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.HubSpot, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.GitHub, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.Ticketing, enabled: false);

            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var platformMessage = env.CreatePlatformMessage(
                room,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            Timestamp = "1111.2222",
                            // Not a supportee, so don't auto-create Conversation
                            // Not the current user's message, thus no announcement option.
                            User = env.TestData.Abbot.User.PlatformUserId,
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))));

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room.Organization);
        }

        [Fact]
        public async Task RendersModalWithMessageWhenInvokedInRoomWeCannotResolve()
        {
            var env = TestEnvironment.Create(snapshot: true);

            // Ignored even though enabled
            await env.EnableIntegrationAsync(IntegrationType.Zendesk);
            await env.EnableIntegrationAsync(IntegrationType.HubSpot);
            await env.EnableIntegrationAsync(IntegrationType.GitHub);
            await env.EnableIntegrationAsync(IntegrationType.Ticketing);

            var platformMessage = env.CreatePlatformMessage(
                null,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            Timestamp = "1111.2222",
                            User = env.TestData.ForeignUser.PlatformUserId // Not the current user's message, thus no announcement option.
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))));

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, platformMessage.Organization);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task RendersModalWithButtonToCloseConversationWhenInvokedOnMessageInConversation(bool inHub, bool rootMessage)
        {
            var env = TestEnvironment.Create(snapshot: true);

            // Ignored because disabled
            await env.CreateIntegrationAsync(IntegrationType.Zendesk, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.HubSpot, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.GitHub, enabled: false);
            await env.CreateIntegrationAsync(IntegrationType.Ticketing, enabled: false);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var (messageId, threadId) = rootMessage
                ? (convo.FirstMessageId, null)
                : (env.IdGenerator.GetSlackMessageId(), convo.FirstMessageId);

            var hub = await env.CreateHubAsync();
            if (inHub)
            {
                convo.Hub = hub;
                convo.HubId = hub.Id;
                convo.HubThreadId = env.IdGenerator.GetSlackMessageId();
                await env.Db.SaveChangesAsync();

                (messageId, threadId) = rootMessage
                    ? (convo.HubThreadId, null)
                    : (env.IdGenerator.GetSlackMessageId(), convo.HubThreadId);
            }

            var platformMessage = env.CreatePlatformMessage(
                inHub ? hub.Room : room,
                triggerId: "the-trigger-id",
                messageId: messageId,
                threadId: threadId);

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo)
                .UseParameters(inHub, rootMessage);
        }

        [Theory]
        [InlineData(Roles.Administrator, true)]
        [InlineData(Roles.Administrator, false)]
        [InlineData(Roles.Agent, true)]
        [InlineData(Roles.Agent, false)]
        [InlineData(null, true)]
        [InlineData(null, false)]
        public async Task RendersNoticeIfZendeskSettingsNotConfigured(
            string role, bool hasConvo)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var organization = env.TestData.Organization;

            var member = await env.CreateMemberInRoleAsync(role);

            await env.EnableIntegrationAsync(IntegrationType.Zendesk);

            var room = await env.CreateRoomAsync(managedConversationsEnabled: false);
            var convo = hasConvo ? await env.CreateConversationAsync(room) : null;
            var platformMessage = env.CreatePlatformMessage(
                room,
                from: member,
                triggerId: "the-trigger-id",
                messageId: convo?.FirstMessageId);

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, organization, convo)
                .UseParameters(role, hasConvo);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RendersLinkToZendeskButtonIfIntegrationEnabledAndHasSettings(bool hasConvo)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var integration = await env.EnableIntegrationAsync(new ZendeskSettings
            {
                Subdomain = "subdomain",
                ApiToken = env.Secret("the-api-token"),
            });

            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = hasConvo ? await env.CreateConversationAsync(room) : null;
            var platformMessage = env.CreatePlatformMessage(
                room,
                triggerId: "the-trigger-id",
                messageId: convo?.FirstMessageId);

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room.Organization, convo)
                .UseParameters(hasConvo);
        }

        [Fact]
        public async Task RendersLinkToZendeskButtonWithHiddenConversationIdWhenRoomIsNotTracked()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var integration = await env.EnableIntegrationAsync(new ZendeskSettings
            {
                Subdomain = "subdomain",
                ApiToken = env.Secret("the-api-token"),
            });

            var room = await env.CreateRoomAsync(managedConversationsEnabled: false);
            var convo = await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);
            var platformMessage = env.CreatePlatformMessage(
                room,
                triggerId: "the-trigger-id",
                messageId: convo.FirstMessageId);

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RendersButtonToOpenZendeskTicketIfExistingLinkPresent(bool hasConvoTracking)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var organization = env.TestData.Organization;
            organization.PlanType = hasConvoTracking ? PlanType.Business : PlanType.None;

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                TestZendeskApiUrl);

            // Show link even if now disabled
            await env.CreateIntegrationAsync(IntegrationType.Zendesk, enabled: false);

            var platformMessage = env.CreatePlatformMessage(
                room,
                triggerId: "the-trigger-id",
                messageId: convo.FirstMessageId);

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo)
                .UseParameters(hasConvoTracking);
        }

        [InlineData(Roles.Administrator, true)]
        [InlineData(Roles.Administrator, false)]
        [InlineData(Roles.Agent, true)]
        [InlineData(Roles.Agent, false)]
        [InlineData(null, true)]
        [InlineData(null, false)]
        public async Task RendersNoticeIfHubSpotSettingsNotConfigured(
            string role, bool hasConvo)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var organization = env.TestData.Organization;

            var member = await env.CreateMemberInRoleAsync(role);

            await env.EnableIntegrationAsync(IntegrationType.HubSpot);

            var room = await env.CreateRoomAsync(managedConversationsEnabled: false);
            var convo = hasConvo ? await env.CreateConversationAsync(room) : null;
            var platformMessage = env.CreatePlatformMessage(
                room,
                from: member,
                triggerId: "the-trigger-id",
                messageId: convo?.FirstMessageId);

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            Assert.True(env.Responder.OpenModals.TryGetValue("the-trigger-id", out var modal));
            Assert.Equal("Manage Conversation", modal.Title);
            Assert.Equal("Close", modal.Close?.Text);
            Assert.Null(modal.Submit);

            await Verify(env, organization, convo)
                .UseParameters(role, hasConvo);
        }

        [Theory]
        [InlineData(RoomFlags.ManagedConversationsEnabled)]
        [InlineData(RoomFlags.Default)]
        public async Task RendersLinkToHubSpotButtonWithConversationIdIfIntegrationEnabledAndHasSettingsAndConversationExists(
            RoomFlags roomFlags)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var integration = await env.EnableIntegrationAsync(
            new HubSpotSettings
            {
                AccessToken = env.Secret("access_token"),
                RefreshToken = env.Secret("refresh_token"),
                RedirectUri = "https://example.com",
                HubDomain = "domain",
            }, externalId: "42");

            var room = await env.CreateRoomAsync(roomFlags);
            // Even for a hidden conversation, this should still use the conversation Id in the button value.
            var convo = await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);
            var platformMessage = env.CreatePlatformMessage(
                room,
                triggerId: "the-trigger-id",
                messageId: convo.FirstMessageId);

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo)
                .UseParameters(roomFlags);
        }

        [Theory]
        [InlineData(RoomFlags.ManagedConversationsEnabled)]
        [InlineData(RoomFlags.Default)]
        public async Task RendersLinkToHubSpotButtonWithRoomAndMessageIdIfIntegrationEnabledAndHasSettingsAndConversationDoesNotExists(
            RoomFlags roomFlags)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var integration = await env.EnableIntegrationAsync(
            new HubSpotSettings
            {
                AccessToken = env.Secret("access_token"),
                RefreshToken = env.Secret("refresh_token"),
                RedirectUri = "https://example.com",
                HubDomain = "domain",
            }, externalId: "42");

            var room = await env.CreateRoomAsync(roomFlags);
            var platformMessage = env.CreatePlatformMessage(
                room,
                triggerId: "the-trigger-id",
                messageId: "some-message-id");

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room.Organization)
                .UseParameters(roomFlags);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RendersButtonToOpenHubSpotTicketIfExistingLinkPresent(bool hasConvoTracking)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var organization = env.TestData.Organization;
            organization.PlanType = hasConvoTracking ? PlanType.Business : PlanType.None;

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.HubSpotTicket,
                TestHubSpotWebUrl);

            // Show link even if now disabled
            await env.CreateIntegrationAsync(IntegrationType.HubSpot, enabled: false);

            var platformMessage = env.CreatePlatformMessage(
                room,
                triggerId: "the-trigger-id",
                messageId: convo.FirstMessageId);

            var handler = env.Activate<ManageConversationHandler>();
            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo)
                .UseParameters(hasConvoTracking);
        }

        [Fact]
        public async Task RendersButtonToOpenAnnouncementsDialogWhenMessageCreatedByAgentInRoomWithBot()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var agent = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync(botIsMember: true);
            var platformMessage = env.CreatePlatformMessage(
                room,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            User = agent.User.PlatformUserId
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))),
                from: agent);
            var handler = env.Activate<ManageConversationHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room.Organization);
        }

        [Fact]
        public async Task HidesButtonToOpenAnnouncementsDialogWhenMessageHasConversation()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var agent = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync(botIsMember: true);
            var convo = await env.CreateConversationAsync(room);
            var platformMessage = env.CreatePlatformMessage(
                room,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            User = agent.User.PlatformUserId
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))),
                messageId: convo.FirstMessageId,
                from: agent);
            var handler = env.Activate<ManageConversationHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo);
        }

        [Fact]
        public async Task HidesButtonToOpenAnnouncementsDialogWhenMessageCreatedNonAgentInRoomWithBot()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var admin = await env.CreateAdminMemberAsync();
            var room = await env.CreateRoomAsync(botIsMember: true);
            var convo = await env.CreateConversationAsync(room);
            var platformMessage = env.CreatePlatformMessage(
                room,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            User = admin.User.PlatformUserId
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))),
                messageId: convo.FirstMessageId,
                from: admin);
            var handler = env.Activate<ManageConversationHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo);
        }

        [Fact]
        public async Task HidesButtonToOpenAnnouncementsDialogWhenMessageCreatedDifferentAgentInRoomWithBot()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var author = await env.CreateMemberInAgentRoleAsync();
            var agent = await env.CreateMemberInAgentRoleAsync();
            var room = await env.CreateRoomAsync(botIsMember: true);
            var convo = await env.CreateConversationAsync(room);
            var platformMessage = env.CreatePlatformMessage(
                room,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            User = author.User.PlatformUserId
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))),
                messageId: convo.FirstMessageId,
                from: agent);
            var handler = env.Activate<ManageConversationHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo);
        }

        [Fact]
        public async Task DoesNotRendersButtonToOpenAnnouncementsDialogWhenMessageCreatedByCurrentUserInRoomWithoutBot()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync(botIsMember: false);
            var convo = await env.CreateConversationAsync(room);
            var platformMessage = env.CreatePlatformMessage(
                room,
                interactionInfo: new MessageInteractionInfo(
                    new MessageActionPayload
                    {
                        TriggerId = "the-trigger-id",
                        Message = new SlackMessage
                        {
                            User = env.TestData.User.PlatformUserId
                        }
                    },
                    string.Empty,
                    new InteractionCallbackInfo(nameof(ManageConversationHandler))),
                messageId: convo.FirstMessageId);
            var handler = env.Activate<ManageConversationHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, convo);
        }
    }
}
