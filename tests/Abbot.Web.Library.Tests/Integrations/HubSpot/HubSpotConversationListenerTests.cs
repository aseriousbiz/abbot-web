using System;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using MassTransit.Internals;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serious.Abbot;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Abbot.Models;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.TestHelpers;
using Xunit;

public class HubSpotConversationListenerTests
{
    public class TheOnNewMessageAsyncMethod
    {
        [Fact]
        public async Task NoOpsIfConversationNotLinkedToTicket()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);

            var listener = env.Activate<HubSpotConversationListener>();
            await listener.OnNewMessageAsync(convo,
                new ConversationMessage(
                    "The message",
                    convo.Organization,
                    env.TestData.ForeignMember,
                    convo.Room,
                    DateTime.UtcNow,
                    "1111",
                    "2222",
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>(),
                    FakeMessageContext.Create()));

            Assert.Empty(env.HubSpotClientFactory.Clients);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task NoOpsIfHubSpotIntegrationNotConfigured(bool? installed, bool hasCredentials)
        {
            var env = TestEnvironment.Create();

            if (installed is not null)
            {
                var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.HubSpot, env.TestData.Member);

                if (hasCredentials)
                {
                    integration.ExternalId = "42";
                    await env.Integrations.SaveSettingsAsync(
                        integration,
                        new HubSpotSettings
                        {
                            AccessToken = env.Secret("access_token"),
                            RefreshToken = env.Secret("refresh_token"),
                            RedirectUri = "https://example.com",
                            HubDomain = "hub_domain",
                        });
                }

                if (!installed.Value)
                {
                    await env.Integrations.DisableAsync(env.TestData.Organization, IntegrationType.HubSpot, env.TestData.Member);
                }
            }

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.HubSpotTicket,
                "https://app.hubspot.com/contacts/11111111/ticket/2222222222");

            var listener = env.Activate<HubSpotConversationListener>();
            await listener.OnNewMessageAsync(convo,
                new ConversationMessage(
                    "The message",
                    convo.Organization,
                    env.TestData.ForeignMember,
                    convo.Room,
                    DateTime.UtcNow,
                    "1111",
                    "2222",
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>(),
                    FakeMessageContext.Create()));

            Assert.Empty(env.HubSpotClientFactory.Clients);
        }

        [Fact]
        public async Task NoOpsIfMessageNotLive()
        {
            var env = TestEnvironment.Create();

            var integration = await env.CreateIntegrationAsync(
                new HubSpotSettings
                {
                    AccessToken = env.Secret("access_token"),
                    RefreshToken = env.Secret("refresh_token"),
                    RedirectUri = "https://example.com",
                    HubDomain = "hub_domain",
                },
                enabled: true,
                externalId: "42");

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.HubSpotTicket,
                "https://app.hubspot.com/contacts/11111111/ticket/2222222222");

            var listener = env.Activate<HubSpotConversationListener>();
            var message = new ConversationMessage(
                "The message",
                convo.Organization,
                env.TestData.ForeignMember,
                convo.Room,
                DateTime.UtcNow,
                "1111",
                "2222",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                MessageContext: null);
            Assert.False(message.IsLive);

            await listener.OnNewMessageAsync(convo, message);

            Assert.Empty(env.HubSpotClientFactory.Clients);
        }

        [Fact]
        public async Task PostsCommentIfHubSpotTimelineEventIsRegistered()
        {
            var builder = TestEnvironmentBuilder.Create();
            builder.Services.Configure<HubSpotOptions>(options => {
                options.TimelineEvents.Add(TimelineEvents.SlackMessagePosted, "99");
            });
            var env = builder.Build();

            env.TestData.ForeignUser.Avatar = "https://example.com/avatar";
            await env.Db.SaveChangesAsync();

            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.HubSpot, env.TestData.Member);
            integration.ExternalId = "42";
            await env.Integrations.SaveSettingsAsync(
                integration,
                new HubSpotSettings
                {
                    AccessToken = env.Secret("access_token"),
                    RefreshToken = env.Secret("refresh_token"),
                    RedirectUri = "https://example.com",
                    HubDomain = "hub_domain",
                });

            var hubSpotClient = env.HubSpotClientFactory.ClientFor("access_token");

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.HubSpotTicket,
                "https://app.hubspot.com/contacts/11111111/ticket/2222222222");

            TimelineEvent? createdEvent = null;
            hubSpotClient.CreateTimelineEventAsync(Arg.Any<TimelineEvent>())
                .Returns(call => {
                    createdEvent = call.Arg<TimelineEvent>();
                    createdEvent.Id = "42";
                    return createdEvent;
                });

            var listener = env.Activate<HubSpotConversationListener>();
            await listener.OnNewMessageAsync(convo,
                new ConversationMessage(
                    "The message",
                    convo.Organization,
                    env.TestData.ForeignMember,
                    convo.Room,
                    DateTime.UtcNow,
                    "1111",
                    "2222",
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>(),
                    FakeMessageContext.Create()));

            Assert.NotNull(createdEvent);
            Assert.Equal("42", createdEvent.Id);
            Assert.Equal("2222222222", createdEvent.ObjectId);
            Assert.Equal("99", createdEvent.EventTemplateId);
            Assert.Equal(
                new (string, object?)[]
                {
                    ("slackAuthorAvatar", "https://example.com/avatar"),
                    ("slackAuthorName", env.TestData.ForeignMember.DisplayName),
                    ("slackAuthorUrl", env.TestData.ForeignMember.FormatPlatformUrl()),
                    ("slackMessage", "The message"),
                    (
                        "slackMessageUrl",
                            SlackFormatter.MessageUrl(env.TestData.Organization.Domain,
                            convo.Room.PlatformRoomId,
                            "1111",
                            "2222")),
                },
                createdEvent.Tokens.OrderBy(kvp => kvp.Key).Select(kvp => (kvp.Key, kvp.Value)).ToArray());
        }
    }

    public class TheOnStateChangedAsyncMethod
    {
        [Fact]
        public async Task NoOpsIfConversationNotLinkedToTicket()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var actor = env.TestData.Member;

            var listener = env.Activate<HubSpotConversationListener>();
            await listener.OnStateChangedAsync(new()
            {
                Conversation = convo,
                ConversationId = convo.Id,
                Member = actor,
                MemberId = actor.Id,
                Created = env.Clock.UtcNow,
                Implicit = false, // Ignored
                OldState = default, // Ignored
                NewState = convo.State, // Ignored, but should always match
            });

            Assert.Empty(env.HubSpotClientFactory.Clients);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task NoOpsIfHubSpotIntegrationNotConfigured(bool? enabled, bool hasCredentials)
        {
            var env = TestEnvironment.Create();

            if (enabled is not null)
            {
                var integration = await env.CreateIntegrationAsync(
                    IntegrationType.HubSpot,
                    enabled: enabled.Value);

                if (hasCredentials)
                {
                    integration.ExternalId = "42";
                    await env.Integrations.SaveSettingsAsync(
                        integration,
                        new HubSpotSettings
                        {
                            AccessToken = env.Secret("access_token"),
                            RefreshToken = env.Secret("refresh_token"),
                            RedirectUri = "https://example.com",
                            HubDomain = "hub_domain",
                        });
                }
            }

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var actor = env.TestData.Member;

            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.HubSpotTicket,
                "https://app.hubspot.com/contacts/11111111/ticket/2222222222");

            var listener = env.Activate<HubSpotConversationListener>();
            await listener.OnStateChangedAsync(new()
            {
                Conversation = convo,
                ConversationId = convo.Id,
                Member = actor,
                MemberId = actor.Id,
                Created = env.Clock.UtcNow,
                Implicit = false, // Ignored
                OldState = default, // Ignored
                NewState = convo.State, // Ignored, but should always match
            });

            Assert.Empty(env.HubSpotClientFactory.Clients);
        }

        [Theory]
        [InlineData("PWrong", ConversationState.New)] // Configured
        [InlineData("PRight", ConversationState.Waiting)] // Not Configured
        [InlineData("PRight", ConversationState.Closed)] // Redundant
        public async Task NoOpsForInvalidOrRedundantPipelineAndStage(string pipeline, ConversationState state)
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.Member;

            var integration = await env.CreateIntegrationAsync(
                new HubSpotSettings
                {
                    AccessToken = env.Secret("access_token"),
                    RefreshToken = env.Secret("refresh_token"),
                    RedirectUri = "https://example.com",
                    HubDomain = "hub_domain",

                    TicketPipelineId = "PRight",
                    NewTicketPipelineStageId = "new",
                    ClosedTicketPipelineStageId = "closed",
                },
                enabled: true);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room, initialState: state);
            await env.Db.SaveChangesAsync();

            var ticketId = "2222222222";
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.HubSpotTicket,
                $"https://app.hubspot.com/contacts/11111111/ticket/{ticketId}");

            var hubSpotClient = env.HubSpotClientFactory.ClientFor("access_token");
            var hubSpotTicket = new HubSpotTicket
            {
                Id = ticketId,
                Properties = new Dictionary<string, string?>
                {
                    ["hs_pipeline"] = pipeline,
                    ["hs_pipeline_stage"] = "closed",
                },
            };
            hubSpotClient.SafelyGetTicketAsync(ticketId).Returns(hubSpotTicket);

            var listener = env.Activate<HubSpotConversationListener>();
            await listener.OnStateChangedAsync(new()
            {
                Conversation = convo,
                ConversationId = convo.Id,
                Member = actor,
                MemberId = actor.Id,
                Created = env.Clock.UtcNow,
                Implicit = false, // Ignored
                OldState = default, // Ignored
                NewState = convo.State, // Ignored, but should always match
            });

            // Sanity check; this test had failed for the wrong reason!
            await hubSpotClient.Received().SafelyGetTicketAsync(ticketId);

            await hubSpotClient.DidNotReceive()
                .UpdateTicketAsync(Arg.Any<string>(), Arg.Any<CreateOrUpdateTicket>());
        }

        [Fact]
        public async Task NoOpsIfActorIsAbbot()
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.Abbot;

            var integration = await env.CreateIntegrationAsync(
                new HubSpotSettings
                {
                    AccessToken = env.Secret("access_token"),
                    RefreshToken = env.Secret("refresh_token"),
                    RedirectUri = "https://example.com",
                    HubDomain = "hub_domain",

                    TicketPipelineId = "pipe",
                    NewTicketPipelineStageId = "new",
                    WaitingTicketPipelineStageId = "them",
                    NeedsResponseTicketPipelineStageId = "us",
                    ClosedTicketPipelineStageId = "done",
                },
                enabled: true);

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.HubSpotTicket,
                "https://app.hubspot.com/contacts/11111111/ticket/2222222222");

            var listener = env.Activate<HubSpotConversationListener>();
            await listener.OnStateChangedAsync(new()
            {
                Conversation = convo,
                ConversationId = convo.Id,
                Member = actor,
                MemberId = actor.Id,
                Created = env.Clock.UtcNow,
                Implicit = false, // Ignored
                OldState = default, // Ignored
                NewState = convo.State, // Ignored, but should always match
            });

            // Should quit before making client even though Integration is configured
            Assert.Empty(env.HubSpotClientFactory.Clients);
        }

        [Theory]
        [InlineData(ConversationState.Unknown, "them", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Unknown, "us", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Unknown, "us", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Unknown, "us", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.New, "new", TestMemberType.HomeMember)]
        [InlineData(ConversationState.New, "new", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.New, "new", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.New, "new", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.NeedsResponse, "us", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.NeedsResponse, "us", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.NeedsResponse, "us", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.Overdue, "us", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Overdue, "us", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Overdue, "us", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.Waiting, "them", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Closed, "done", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Archived, "them", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Archived, "us", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Archived, "us", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Archived, "us", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.Snoozed, "us", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Snoozed, "us", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Snoozed, "us", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Snoozed, "us", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.Hidden, "them", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Hidden, "us", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Hidden, "us", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Hidden, "us", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        public async Task SetsHubSpotPipelineStatus(
            ConversationState state,
            string expectedStage,
            TestMemberType actorType,
            RoomFlags roomFlags = default)
        {
            var env = TestEnvironment.Create();
            var actor = env.TestData.GetMember(actorType);

            var integration = await env.CreateIntegrationAsync(
                new HubSpotSettings
                {
                    AccessToken = env.Secret("access_token"),
                    RefreshToken = env.Secret("refresh_token"),
                    RedirectUri = "https://example.com",
                    HubDomain = "hub_domain",

                    TicketPipelineId = "pipe",
                    NewTicketPipelineStageId = "new",
                    WaitingTicketPipelineStageId = "them",
                    NeedsResponseTicketPipelineStageId = "us",
                    ClosedTicketPipelineStageId = "done",
                },
                enabled: true);

            var room = await env.CreateRoomAsync(roomFlags);
            var convo = await env.CreateConversationAsync(room, initialState: state);
            await env.Db.SaveChangesAsync();

            var ticketId = "2222222222";
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.HubSpotTicket,
                $"https://app.hubspot.com/contacts/11111111/ticket/{ticketId}");

            var hubSpotClient = env.HubSpotClientFactory.ClientFor("access_token");
            var hubSpotTicket = new HubSpotTicket
            {
                Id = ticketId,
                Properties = new Dictionary<string, string?>
                {
                    ["hs_pipeline"] = "pipe",
                    ["hs_pipeline_stage"] = "change-me",
                },
            };
            hubSpotClient.SafelyGetTicketAsync(ticketId).Returns(hubSpotTicket);

            IDictionary<string, string?>? updateProperties = null;
            hubSpotClient.UpdateTicketAsync(Arg.Is(ticketId), Arg.Any<CreateOrUpdateTicket>())
                .Returns(async call => {
                    updateProperties = call.Arg<CreateOrUpdateTicket>().Properties;
                    return new HubSpotTicket
                    {
                        Id = ticketId,
                        Properties = hubSpotTicket.Properties.MergeLeft(updateProperties)
                    };
                });

            var listener = env.Activate<HubSpotConversationListener>();
            await listener.OnStateChangedAsync(new()
            {
                Conversation = convo,
                ConversationId = convo.Id,
                Member = actor,
                MemberId = actor.Id,
                Created = env.Clock.UtcNow,
                Implicit = false, // Ignored
                OldState = default, // Ignored
                NewState = convo.State, // Ignored, but should always match
            });

            Assert.NotNull(updateProperties);
            var actualStage = Assert.Contains("hs_pipeline_stage", updateProperties);
            Assert.Equal(expectedStage, actualStage);
        }
    }
}
