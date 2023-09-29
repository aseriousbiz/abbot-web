using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious.Abbot;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Models;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.TestHelpers;
using Xunit;

public class SlackToZendeskConversationListenerTests
{
    public class TheOnNewMessageAsyncMethod
    {
        [Fact]
        public async Task NoOpsIfConversationNotLinkedToTicket()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);

            var listener = env.Activate<SlackToZendeskConversationListener>();
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

            Assert.Empty(env.ZendeskClientFactory.Clients);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task NoOpsIfZendeskIntegrationNotConfigured(bool? installed, bool hasCredentials)
        {
            var env = TestEnvironment.Create();

            if (installed is not null)
            {
                var integration =
                    await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

                if (hasCredentials)
                {
                    await env.Integrations.SaveSettingsAsync(integration,
                        new ZendeskSettings()
                        {
                            Subdomain = "test",
                            ApiToken = env.Secret("the-token"),
                        });
                }

                if (!installed.Value)
                {
                    await env.Integrations.DisableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
                }
            }

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                "https://test.zendesk.com/api/v2/tickets/123.json");

            var listener = env.Activate<SlackToZendeskConversationListener>();
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

            Assert.Empty(env.ZendeskClientFactory.Clients);
        }

        [Fact]
        public async Task NoOpsIfNoZendeskIdentityCanBeResolved()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = env.Secret("the-token"),
                });

            var zendeskClient = env.ZendeskClientFactory.ClientFor("test");

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                "https://test.zendesk.com/api/v2/tickets/123.json");

            zendeskClient.Tickets[123] = (
                new ZendeskTicket
                {
                    Subject = "the ticket",
                },
                new List<Comment>
                {
                    new()
                    {
                        HtmlBody = "Comment 1"
                    },
                    new()
                    {
                        HtmlBody = "Comment 2"
                    },
                });

            resolver.ResolveZendeskIdentityAsync(
                    zendeskClient,
                    env.TestData.Organization,
                    env.TestData.ForeignMember,
                    null)
                .Returns(Task.FromResult<ZendeskUser?>(null));

            var listener = env.Activate<SlackToZendeskConversationListener>();
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

            Assert.Equal(
                new[] { "Comment 1", "Comment 2" },
                zendeskClient.Tickets[123].Comments.Select(c => c.HtmlBody).ToArray());
        }

        [Fact]
        public async Task NoOpsIfMessageNotLive()
        {
            // We don't send comments to closed tickets.
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = env.Secret("the-token"),
                });

            var zendeskClient = env.ZendeskClientFactory.ClientFor("test");

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(
                convo,
                ConversationLinkType.ZendeskTicket,
                "https://test.zendesk.com/api/v2/tickets/123.json");

            zendeskClient.Tickets[123] = (
                new ZendeskTicket
                {
                    Subject = "the ticket",
                },
                new List<Comment>
                {
                    new()
                    {
                        AuthorId = 1,
                        HtmlBody = "Comment 1"
                    },
                    new()
                    {
                        AuthorId = 2,
                        HtmlBody = "Comment 2"
                    },
                });

            resolver.ResolveZendeskIdentityAsync(
                    zendeskClient,
                    env.TestData.Organization,
                    env.TestData.ForeignMember,
                    null)
                .Returns(new ZendeskUser()
                {
                    Url = new ZendeskUserLink("test", 456).ApiUrl.ToString(),
                });

            var listener = env.Activate<SlackToZendeskConversationListener>();
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

            Assert.Equal(
                new[] { "Comment 1", "Comment 2" },
                zendeskClient.Tickets[123].Comments.Select(c => c.HtmlBody).ToArray());
        }

        [Fact]
        public async Task NoOpsIfZendeskTicketStatusClosed()
        {
            // We don't send comments to closed tickets.
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = env.Secret("the-token"),
                });

            var zendeskClient = env.ZendeskClientFactory.ClientFor("test");

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(
                convo,
                ConversationLinkType.ZendeskTicket,
                "https://test.zendesk.com/api/v2/tickets/123.json");

            zendeskClient.Tickets[123] = (
                new ZendeskTicket
                {
                    Subject = "the ticket",
                    Status = "closed",
                },
                new List<Comment>
                {
                    new()
                    {
                        AuthorId = 1,
                        HtmlBody = "Comment 1"
                    },
                    new()
                    {
                        AuthorId = 2,
                        HtmlBody = "Comment 2"
                    },
                });

            resolver.ResolveZendeskIdentityAsync(
                    zendeskClient,
                    env.TestData.Organization,
                    env.TestData.ForeignMember,
                    null)
                .Returns(new ZendeskUser()
                {
                    Url = new ZendeskUserLink("test", 456).ApiUrl.ToString(),
                });

            var listener = env.Activate<SlackToZendeskConversationListener>();
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

            Assert.Equal(
                new[] { "Comment 1", "Comment 2" },
                zendeskClient.Tickets[123].Comments.Select(c => c.HtmlBody).ToArray());
        }

        [Fact]
        public async Task PostsCommentIfZendeskIsLinkedAndIdentityCanBeResolved()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var integration = await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = env.Secret("the-token"),
                });

            var zendeskClient = env.ZendeskClientFactory.ClientFor("test");

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                "https://test.zendesk.com/api/v2/tickets/123.json");

            zendeskClient.Tickets[123] = (
                new ZendeskTicket
                {
                    Subject = "the ticket",
                },
                new List<Comment>()
                {
                    new()
                    {
                        AuthorId = 1,
                        HtmlBody = "Comment 1"
                    },
                    new()
                    {
                        AuthorId = 2,
                        HtmlBody = "Comment 2"
                    },
                });

            resolver.ResolveZendeskIdentityAsync(
                    zendeskClient,
                    env.TestData.Organization,
                    env.TestData.ForeignMember,
                    null)
                .Returns(new ZendeskUser()
                {
                    Id = 456,
                    Url = new ZendeskUserLink("test", 456).ApiUrl.ToString(),
                });

            var listener = env.Activate<SlackToZendeskConversationListener>();
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

            var userUrl = env.TestData.ForeignMember.FormatPlatformUrl();
            var messageUrl = SlackFormatter.MessageUrl(env.TestData.Organization.Domain,
                convo.Room.PlatformRoomId,
                "1111",
                "2222");

            var expectedCommentBody = $@"
<strong><a href=""{messageUrl}"">New reply</a> from <a href=""{userUrl}"">{env.TestData.ForeignMember.DisplayName}</a> in Slack:</strong><br />
The message";

            Assert.Equal(
                new (long, string?)[] { (1, "Comment 1"), (2, "Comment 2"), (456, expectedCommentBody) },
                zendeskClient.Tickets[123].Comments.Select(c => (c.AuthorId, c.HtmlBody)).ToArray());
        }
    }

    public class TheOnStateChangedAsyncMethod
    {
        [Fact]
        public async Task NoOpsIfActorIsAbbot()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var actor = env.TestData.Abbot;

            var integration = await env.EnableIntegrationAsync(
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = env.Secret("the-token"),
                });

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room, initialState: ConversationState.Closed);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                "https://test.zendesk.com/api/v2/tickets/123.json");

            var listener = env.Activate<SlackToZendeskConversationListener>();
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
            Assert.Empty(env.ZendeskClientFactory.Clients);
        }

        [Theory]
        [InlineData(ConversationState.Unknown, "pending", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Unknown, "open", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Unknown, "open", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Unknown, "open", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.New, "open", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.New, "open", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.New, "open", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.NeedsResponse, "open", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.NeedsResponse, "open", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.NeedsResponse, "open", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.Overdue, "open", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Overdue, "open", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Overdue, "open", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.Waiting, "pending", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Closed, "solved", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Archived, "pending", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Archived, "open", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Archived, "open", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Archived, "open", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.Snoozed, "open", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Snoozed, "open", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Snoozed, "open", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Snoozed, "open", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(ConversationState.Hidden, "pending", TestMemberType.HomeMember)]
        [InlineData(ConversationState.Hidden, "open", TestMemberType.HomeGuest)]
        [InlineData(ConversationState.Hidden, "open", TestMemberType.ForeignMember)]
        [InlineData(ConversationState.Hidden, "open", TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        public async Task SetsZendeskTicketStatus(
            ConversationState state,
            string expectedStatus,
            TestMemberType actorType,
            RoomFlags roomFlags = default)
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var actor = env.TestData.GetMember(actorType);

            var integration = await env.Integrations.EnableAsync(
                env.TestData.Organization,
                IntegrationType.Zendesk,
                env.TestData.Member);
            await env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = env.Secret("the-token"),
                });

            var zendeskClient = env.ZendeskClientFactory.ClientFor("test");

            var room = await env.CreateRoomAsync(roomFlags);
            var convo = await env.CreateConversationAsync(room);
            convo.State = state;
            await env.Db.SaveChangesAsync();
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                "https://test.zendesk.com/api/v2/tickets/123.json");

            zendeskClient.Tickets[123] = (
                new ZendeskTicket
                {
                    Subject = "the ticket",
                    Status = "change-me",
                },
                new List<Comment>()
                {
                    new()
                    {
                        AuthorId = 1,
                        HtmlBody = "Comment 1"
                    },
                    new()
                    {
                        AuthorId = 2,
                        HtmlBody = "Comment 2"
                    },
                });

            resolver.ResolveZendeskIdentityAsync(
                    zendeskClient,
                    env.TestData.Organization,
                    actor,
                    null)
                .Returns(new ZendeskUser()
                {
                    Url = new ZendeskUserLink("test", 456).ApiUrl.ToString(),
                });

            var listener = env.Activate<SlackToZendeskConversationListener>();
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

            Assert.Equal(expectedStatus, zendeskClient.Tickets[123].Ticket.Status);
        }

        [Theory]
        [InlineData("new", ConversationState.New)]
        [InlineData("open", ConversationState.New)]
        [InlineData("pending", ConversationState.New, "open")]
        [InlineData("solved", ConversationState.New, "open")]
        [InlineData("closed", ConversationState.New)]
        [InlineData("closed", ConversationState.NeedsResponse)]
        [InlineData("closed", ConversationState.Overdue)]
        [InlineData("closed", ConversationState.Archived)]
        [InlineData("closed", ConversationState.Snoozed)]
        public async Task HandlesNewAndClosedZendeskTicketStatus(
            string currentStatus,
            ConversationState state,
            string? expectedStatus = null)
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var actor = env.TestData.ForeignMember;

            var integration = await env.EnableIntegrationAsync(
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = env.Secret("the-token"),
                });

            var zendeskClient = env.ZendeskClientFactory.ClientFor("test");

            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room, initialState: state);
            await env.CreateConversationLinkAsync(convo,
                ConversationLinkType.ZendeskTicket,
                "https://test.zendesk.com/api/v2/tickets/123.json");

            zendeskClient.Tickets[123] = (
                new ZendeskTicket
                {
                    Subject = "the ticket",
                    Status = currentStatus,
                },
                new List<Comment>()
                {
                    new()
                    {
                        AuthorId = 1,
                        HtmlBody = "Comment 1"
                    },
                });

            resolver.ResolveZendeskIdentityAsync(
                    zendeskClient,
                    env.TestData.Organization,
                    actor,
                    null)
                .Returns(new ZendeskUser()
                {
                    Url = new ZendeskUserLink("test", 456).ApiUrl.ToString(),
                });

            if (expectedStatus is null)
            {
                zendeskClient.ThrowOn(
                    nameof(IZendeskClient.UpdateTicketAsync),
                    new InvalidOperationException("Ticket should not be updated!"));

                expectedStatus = currentStatus;
            }

            var listener = env.Activate<SlackToZendeskConversationListener>();
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

            Assert.Equal(expectedStatus, zendeskClient.Tickets[123].Ticket.Status);
        }
    }
}
