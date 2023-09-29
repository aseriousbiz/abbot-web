using System.Net;
using Abbot.Common.TestHelpers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.GitHub;
using Serious.TestHelpers;
// We never resolve GitHubLinker directly
using TicketLinker = Serious.Abbot.Integrations.ITicketLinker<
        Serious.Abbot.Integrations.GitHub.GitHubSettings>;

public class GitHubLinkerTests
{
    public static IntegrationLink TestGitHubLink =
        GitHubIssueLink.Parse("https://api.github.com/repos/abbot/test/issues/123").Require();

    static readonly GitHubSettings TestSettings = new()
    {
        InstallationId = 54321,
        DefaultRepository = "abbot/test",
    };

    static Dictionary<string, object?> TestProperties => new()
    {
        ["title"] = "Subject",
        ["body"] = "Description",
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

            var client = env.GitHubClientFactory.ClientFor(settings);
            var ex = CreateApiException(HttpStatusCode.Unauthorized);
            client.Issue.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NewIssue>())
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

            var client = env.GitHubClientFactory.ClientFor(settings);
            var ex = CreateApiException(HttpStatusCode.BadRequest);
            client.Issue.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NewIssue>())
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
            Assert.Null(ticketError.UserErrorInfo);
            Assert.Equal(actual.ApiError.ToString(), ticketError.ExtraInfo);
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
            var client = env.GitHubClientFactory.ClientFor(settings);
            var ex = new Exception("Boom");
            client.Issue.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NewIssue>())
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

        [Theory]
        [InlineData(null, "Default Repository is not configured.")]
        [InlineData("", "Default Repository is not configured.")]
        [InlineData("oops", "Repository 'oops' is not valid; expected 'owner/repo'.")]
        [InlineData("oops/", "Repository 'oops/' is not valid; expected 'owner/repo'.")]
        [InlineData("/oops", "Repository '/oops' is not valid; expected 'owner/repo'.")]
        [InlineData("owner/repo/oops", "Repository 'owner/repo/oops' is not valid; expected 'owner/repo'.")]
        public async Task ThrowsTicketConfigurationExceptionIfRepositoryNotInPropertiesAndDefaultRepositoryNotSetOrInvalid(string? defaultRepository, string expectedError)
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = new GitHubSettings
            {
                InstallationId = TestSettings.InstallationId,
                DefaultRepository = defaultRepository,
            };
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties,
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<TicketConfigurationException>(() => task);

            Assert.Empty(env.GitHubClientFactory.Clients);
            Assert.Equal(expectedError, actual.Message);
            Assert.Equal(TicketErrorReason.Configuration, actual.Reason);
        }

        [Theory]
        [InlineData(null, "Repository '' is not valid; expected 'owner/repo'.")]
        [InlineData("", "Repository '' is not valid; expected 'owner/repo'.")]
        [InlineData("oops", "Repository 'oops' is not valid; expected 'owner/repo'.")]
        [InlineData("oops/", "Repository 'oops/' is not valid; expected 'owner/repo'.")]
        [InlineData("/oops", "Repository '/oops' is not valid; expected 'owner/repo'.")]
        [InlineData("owner/repo/oops", "Repository 'owner/repo/oops' is not valid; expected 'owner/repo'.")]
        public async Task ThrowsTicketConfigurationExceptionIfRepositoryInPropertiesButInvalid(string? formRepository, string expectedError)
        {
            var env = TestEnvironment.Create();

            var actor = env.TestData.Member;
            var settings = new GitHubSettings
            {
                InstallationId = TestSettings.InstallationId,
            };
            var integration = await env.CreateIntegrationAsync(
                settings,
                enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linker = env.Get<TicketLinker>();

            var task = linker.CreateTicketLinkAsync(
                integration,
                settings,
                TestProperties.With("repository", (object?)formRepository),
                convo,
                actor);
            var actual = await Assert.ThrowsAsync<TicketConfigurationException>(() => task);

            Assert.Empty(env.GitHubClientFactory.Clients);
            Assert.Equal(expectedError, actual.Message);
            Assert.Equal(TicketErrorReason.Configuration, actual.Reason);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember, null)]
        [InlineData(TestMemberType.ForeignMember, null)]
        [InlineData(TestMemberType.HomeGuest, null)]
        [InlineData(TestMemberType.HomeMember, "form/repo")]
        [InlineData(TestMemberType.ForeignMember, "form/repo")]
        [InlineData(TestMemberType.HomeGuest, "form/repo")]
        public async Task CreatesTicketLinkForValidRepository(TestMemberType actorType, string? formRepository)
        {
            var env = TestEnvironment.Create();
            var now = env.Clock.Freeze();

            var actor = env.TestData.GetMember(actorType);
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var client = env.GitHubClientFactory.ClientFor(settings);

            Issue? response = null;
            client.Issue.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NewIssue>())
                .Returns(call => {
                    response = env.GitHubClientFactory.CreateFakeIssue(
                        $"{call.ArgAt<string>(0)}/{call.ArgAt<string>(1)}",
                        call.ArgAt<NewIssue>(2));
                    return response;
                });

            var linker = env.Get<TicketLinker>();

            var fields = TestProperties;

            if (formRepository is not null)
            {
                fields.Add("repository", formRepository);
            }

            var link = await linker.CreateTicketLinkAsync(
                integration,
                settings,
                fields,
                convo,
                actor);

            Assert.NotNull(link);
            Assert.Same(convo, link.Conversation);
            Assert.Equal(ConversationLinkType.GitHubIssue, link.LinkType);
            Assert.Same(actor, link.CreatedBy);
            Assert.Equal(now, link.Created);

            var expectedRepository = formRepository ?? settings.DefaultRepository;
            Assert.NotNull(response);
            Assert.Equal(
                $"https://api.github.com/repos/{expectedRepository}/issues/{response.Number}",
                link.ExternalId);
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData(null, "", null)]
        [InlineData(null, "footer!", "\n\nfooter!")]
        [InlineData("", null, "")]
        [InlineData("", "", "")]
        [InlineData("", "footer!", "\n\nfooter!")]
        [InlineData("body!", null, "body!")]
        [InlineData("body!", "", "body!")]
        [InlineData("body!", "footer!", "body!\n\nfooter!")]
        public async Task CreatesTicketLinkWithFooter(object? body, object? footer, string? expectedBody)
        {
            var env = TestEnvironment.Create();
            var now = env.Clock.Freeze();

            var actor = env.TestData.Member;
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var client = env.GitHubClientFactory.ClientFor(settings);

            Issue? response = null;
            client.Issue.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NewIssue>())
                .Returns(call => {
                    response = env.GitHubClientFactory.CreateFakeIssue(
                        $"{call.ArgAt<string>(0)}/{call.ArgAt<string>(1)}",
                        call.ArgAt<NewIssue>(2));
                    return response;
                });

            var linker = env.Get<TicketLinker>();

            var fields = TestProperties.With("body", body);

            if (footer is not null)
            {
                fields.Add("footer", footer);
            }

            var link = await linker.CreateTicketLinkAsync(
                integration,
                settings,
                fields,
                convo,
                actor);

            Assert.NotNull(link);
            Assert.Same(convo, link.Conversation);
            Assert.Equal(ConversationLinkType.GitHubIssue, link.LinkType);
            Assert.Same(actor, link.CreatedBy);
            Assert.Equal(now, link.Created);

            Assert.NotNull(response);
            Assert.Equal(expectedBody, response.Body);
        }
    }

    public static ApiException CreateApiException(HttpStatusCode statusCode)
    {
        return new ApiException("oops", statusCode);
    }
}
