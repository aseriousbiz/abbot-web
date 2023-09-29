using System.Diagnostics;
using System.Net;
using Abbot.Common.TestHelpers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Integrations.MergeDev.Models;
using Serious.Abbot.Serialization;
using Serious.Cryptography;
using Serious.TestHelpers;
// We never resolve MergeDevLinker directly
using TicketLinker = Serious.Abbot.Integrations.ITicketLinker<
        Serious.Abbot.Integrations.MergeDev.TicketingSettings>;

public class MergeDevLinkerTests
{
    static readonly TicketingSettings TestSettings = new()
    {
        AccessToken = new SecretString("tm-account-token", new FakeDataProtectionProvider()),
        AccountDetails = new()
        {
            Integration = "Ticketmaster",
            IntegrationSlug = "tm",
        },
    };

    static Dictionary<string, object?> TestProperties => new()
    {
        ["name"] = "Subject",
        ["description"] = "Description",
    };

    public class TheCreateTicketLinkAsyncMethod
    {
        [Fact]
        public async Task ThrowsIfCredentialsInvalid()
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.MergeDevClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            var ex = await env.CreateApiExceptionAsync(
                HttpStatusCode.Unauthorized,
                HttpMethod.Post,
                "",
                new {
                    detail = "Invalid Account Token.",
                });
            client.CreateTicketAsync(Arg.Any<Create>())
                .Throws(ex);

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
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);

            var client = env.MergeDevClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            var ex = await env.CreateApiExceptionAsync(
                HttpStatusCode.BadRequest,
                HttpMethod.Post,
                "",
                new {
                    errors = new[]
                    {
                        new {
                            detail = "This request is missing a field required to post a ticketing.Ticket to Ticketmaster: name",
                        },
                    },
                });
            client.CreateTicketAsync(Arg.Any<Create>())
                .Throws(ex);

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
            Assert.Equal(actual.Content, ticketError.UserErrorInfo);
            Assert.Equal(actual.Content, ticketError.ExtraInfo);
        }

        [Fact]
        public async Task ThrowsIfApiFailsForOtherReason()
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.Unauthorized);
            var client = env.MergeDevClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            var ex = new Exception("Boom");
            client.CreateTicketAsync(Arg.Any<Create>())
                .Throws(ex);

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
        public async Task ReportErrorIfApiIndicatesSuccessButModelIsMissing()
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var mockResponse = Substitute.For<HttpWebResponse>();
            mockResponse.StatusCode.Returns(HttpStatusCode.OK);

            var client = env.MergeDevClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            client.CreateTicketAsync(Arg.Any<Create>())
                .Returns(call => new ModelEnvelope<MergeDevTicket>(null));

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            await Assert.ThrowsAsync<UnreachableException>(() => task);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember)]
        [InlineData(TestMemberType.ForeignMember)]
        [InlineData(TestMemberType.HomeGuest)]
        public async Task CreatesTicketLink(TestMemberType actorType)
        {
            var env = TestEnvironment.Create();
            var now = env.Clock.Freeze();

            var actor = env.TestData.GetMember(actorType);
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var client = env.MergeDevClientFactory.ClientFor(TestSettings.AccessToken?.Reveal());
            MergeDevTicket? response = null;
            client.CreateTicketAsync(Arg.Any<Create>())
                .Returns(call => {
                    var request = call.Arg<Create>();
                    response = new MergeDevTicket()
                    {
                        Id = "42",
                        RemoteId = "420",
                        Name = (string?)request.Model.GetValueOrDefault("name"),
                        Description = (string?)request.Model.GetValueOrDefault("description"),
                        Priority = (string?)request.Model.GetValueOrDefault("priority"),
                        TicketUrl = "https://ticketmaster.dev/org/repo/issues/4",
                    };

                    return new(response);
                });

            var linker = env.Get<TicketLinker>();

            var fields = TestProperties.With("priority", (object?)"HIGH");

            var link = await linker.CreateTicketLinkAsync(
                integration,
                settings,
                fields,
                convo,
                actor);

            Assert.NotNull(link);
            Assert.Same(convo, link.Conversation);
            Assert.Equal(ConversationLinkType.MergeDevTicket, link.LinkType);
            Assert.Same(actor, link.CreatedBy);
            Assert.Equal(now, link.Created);

            var linkSettings = JsonSettings.FromJson<MergeDevTicketLink.Settings>(link.Settings);
            Assert.NotNull(linkSettings);
            Assert.Equal(integration, linkSettings.IntegrationId);
            Assert.Equal(settings.IntegrationSlug, linkSettings.IntegrationSlug);
            Assert.Equal(settings.IntegrationName, linkSettings.IntegrationName);

            Assert.NotNull(response);
            Assert.Equal(response.Id, link.ExternalId);
            Assert.Equal(response.TicketUrl, linkSettings.ExternalWebUrl);
            Assert.Equal("Subject", response.Name);
            Assert.Equal("Description", response.Description);
            Assert.Equal("HIGH", response.Priority);
        }
    }
}
