using System.Diagnostics;
using System.Net;
using Abbot.Common.TestHelpers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.Exceptions;
using Refit;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Signals;
using Serious.Cryptography;
using Serious.TestHelpers;

// We never resolve MergeDevLinker directly
using TicketLinker = Serious.Abbot.Integrations.ITicketLinker<
        Serious.Abbot.Integrations.Zendesk.ZendeskSettings>;

public class ZendeskLinkerTests
{
    static readonly ZendeskSettings TestSettings = new ZendeskSettings()
    {
        Subdomain = "subdomain",
        ApiToken = new SecretString("a-secret-token", new FakeDataProtectionProvider()),
    };

    static Dictionary<string, object?> TestProperties => new()
    {
        ["subject"] = "Subject",
        ["comment"] = "Description",
    };

    public class TheCreateTicketLinkAsyncMethod
    {
        [Fact]
        public async Task ReportErrorIfUnableToResolveZendeskUser()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);
            resolver.ResolveZendeskIdentityAsync(
                    env.ZendeskClientFactory.ClientFor(TestSettings.Subdomain),
                    env.TestData.Organization,
                    convo.StartedBy,
                    null)
                .Returns(Task.FromResult<ZendeskUser?>(null));

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.ZendeskClientFactory.ClientFor(TestSettings.Subdomain);
            client.ThrowOn(nameof(IZendeskClient.CreateTicketAsync),
                new WebException("Poop", null, WebExceptionStatus.Success, mockResponse));

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<TicketConfigurationException>(() => task);

            Assert.Equal("I couldn't create the necessary Zendesk users for this conversation.", actual.Message);
            Assert.Equal(TicketErrorReason.UserConfiguration, actual.Reason);
        }

        [Fact]
        public async Task ThrowsIfCredentialsInvalid()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);
            resolver.ResolveZendeskIdentityAsync(
                env.ZendeskClientFactory.ClientFor(TestSettings.Subdomain),
                env.TestData.Organization,
                convo.StartedBy,
                null)
                .Returns(new ZendeskUser()
                {
                    Url = new ZendeskUserLink("subdomain", 42).ApiUrl.ToString(),
                });

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.ZendeskClientFactory.ClientFor(TestSettings.Subdomain);
            var ex = client.ThrowOn(nameof(IZendeskClient.CreateTicketAsync),
                HttpStatusCode.Unauthorized,
                HttpMethod.Post,
                "",
                new object());

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<ApiException>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.Unauthorized, ticketError.Reason);
            Assert.Null(ticketError.UserErrorInfo);
            Assert.Null(ticketError.ExtraInfo);
        }

        [Fact]
        public async Task ThrowsIfApiFailsWithApiException()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);
            resolver.ResolveZendeskIdentityAsync(
                env.ZendeskClientFactory.ClientFor(TestSettings.Subdomain),
                env.TestData.Organization,
                convo.StartedBy,
                null)
                .Returns(new ZendeskUser()
                {
                    Url = new ZendeskUserLink("subdomain", 42).ApiUrl.ToString(),
                });

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.ZendeskClientFactory.ClientFor(TestSettings.Subdomain);
            var ex = client.ThrowOn(nameof(IZendeskClient.CreateTicketAsync),
                HttpStatusCode.BadRequest,
                HttpMethod.Post,
                "",
                new {
                    errors = "???",
                });

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<ApiException>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.ApiError, ticketError.Reason);
            Assert.Null(ticketError.UserErrorInfo);
            Assert.Equal(actual.Content, ticketError.ExtraInfo);
        }

        [Fact]
        public async Task ThrowsIfApiFailsForOtherReason()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IZendeskResolver>(out var resolver)
                .Build();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);
            resolver.ResolveZendeskIdentityAsync(
                env.ZendeskClientFactory.ClientFor(TestSettings.Subdomain),
                env.TestData.Organization,
                convo.StartedBy,
                null)
                .Returns(new ZendeskUser()
                {
                    Url = new ZendeskUserLink("subdomain", 42).ApiUrl.ToString(),
                });

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);
            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            var ex = new Exception("Boom");
            client.ThrowOn(nameof(IZendeskClient.CreateTicketAsync), ex);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<Exception>(() => task);

            Assert.Same(ex, actual);

            var ticketError = linker.ParseException(actual);
            Assert.Equal(TicketErrorReason.Unknown, ticketError.Reason);
            Assert.Null(ticketError.UserErrorInfo);
            Assert.Null(ticketError.ExtraInfo);
        }

        [Fact]
        public async Task CreatesTicketLink()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IMessageRenderer, MessageRenderer>()
                .Substitute<IZendeskResolver>(out var resolver)
                .Substitute<ISlackThreadExporter>(out var threadExporter)
                .Build();
            var now = env.Clock.Freeze();

            env.TestData.Organization.Domain = "aseriousbiz.slack.com";
            await env.Db.SaveChangesAsync();

            var actor = env.TestData.Member;
            var member = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);
            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            resolver.ResolveZendeskIdentityAsync(
                client,
                env.TestData.Organization,
                convo.StartedBy,
                null)
                .Returns(new ZendeskUser()
                {
                    Id = 42,
                    Url = new ZendeskUserLink("subdomain", 42).ApiUrl.ToString(),
                });

            Setting? exportSetting = null;
            threadExporter.ExportThreadAsync(
                convo.FirstMessageId,
                room.PlatformRoomId,
                convo.Organization,
                actor)
                .Returns(call => exportSetting = new Setting
                {
                    Scope = SettingsScope.Organization(call.Arg<Organization>()).Name,
                    Name = $"Slack.Thread.Export:{call.ArgAt<string>(0)}:{call.ArgAt<string>(1)}",
                    Value = "{}",
                    Creator = call.Arg<Member>().User,
                });

            var linker = env.Get<TicketLinker>();

            var link = await linker.CreateTicketLinkAsync(
                integration,
                settings,
                new Dictionary<string, object?>
                {
                    ["subject"] = "Subject",
                    ["comment"] = $"A {member.ToMention()} in the room {room.ToMention()}.",
                    ["priority"] = "high",
                    ["tags"] = "a,b,c",
                    ["type"] = "problem",
                    ["custom_field:123"] = "custom_value",
                    ["custom_field:456"] = "another_value",
                    ["custom_field:789"] = new[] { "one", "two", "three" },
                },
                convo,
                actor);

            Assert.NotNull(link);
            Assert.Same(convo, link.Conversation);
            Assert.Equal(ConversationLinkType.ZendeskTicket, link.LinkType);
            Assert.Same(actor, link.CreatedBy);
            Assert.Equal(now, link.Created);

            var expectedUserUrl = SlackFormatter.UserUrl(env.TestData.Organization.Domain,
                env.TestData.User.PlatformUserId);

            var expectedBody =
                $"""
                A <a href="https://aseriousbiz.slack.com/team/{member.User.PlatformUserId}">{member.DisplayName}</a> in the room <a href="https://aseriousbiz.slack.com/archives/{room.PlatformRoomId}">#{room.Name}</a>.
                <p style="margin-top: 2rem">
                    <em>
                        Created by <a href="{expectedUserUrl}">{env.TestData.Member.DisplayName}</a> from this <a href="{convo.GetFirstMessageUrl()}">Slack thread</a>.
                        &bull; <a href="https://app.ab.bot/conversations/{convo.Id}">View on ab.bot</a>
                    </em>
                </p>
                """;

            var (ticket, _) = Assert.Single(client.Tickets.Values);
            Assert.Equal(ticket.Url, link.ExternalId);
            Assert.Equal("Subject", ticket.Subject);
            Assert.Equal(expectedBody, ticket.Comment?.HtmlBody);
            Assert.Equal(42, ticket.RequesterId);
            Assert.Equal(new[] { "a", "b", "c" }, ticket.Tags.ToArray());
            Assert.Equal("problem", ticket.Type);
            Assert.Equal("high", ticket.Priority);
            var actual = ticket.CustomFields.ToDictionary(cf => cf.Id, cf => cf.Value);
            Assert.Equal("custom_value", actual[123]);
            Assert.Equal("another_value", actual[456]);
            Assert.Equal(new[] { "one", "two", "three" }, actual[789]);
            Assert.Equal(3, actual.Count);

            Assert.NotNull(exportSetting);
            env.BackgroundJobClient.DidEnqueue<ZendeskLinkedConversationThreadImporter>(
                importer => importer.ImportThreadAsync(link, exportSetting.Name));
        }
    }
}
