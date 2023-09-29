using System.Diagnostics;
using System.Net;
using Abbot.Common.TestHelpers;
using Abbot.Common.TestHelpers.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Serious.Abbot.Models;
using Serious.Abbot.Routing;
using Serious.Abbot.Services;
using Serious.TestHelpers;

public class TicketLinkerJobTests
{
    const IntegrationType TestIntegrationType = (IntegrationType)99999;
    const ConversationLinkType TestConversationLinkType = (ConversationLinkType)98989;

    public static IntegrationLink TestTicketLink = new FakeTicketLink("123");

    static readonly FakeSettings TestSettings = new() { HasApiCredentials = true };

    static Dictionary<string, object?> TestProperties => new()
    {
        ["title"] = "Subject",
        ["body"] = "Description",
    };

    static SettingsTask Verify(TestEnvironmentWithData env, Integration integration, Conversation? convo) =>
        Verifier.Verify(BuildTarget(env, integration, convo));

    static async Task<object> BuildTarget(TestEnvironmentWithData env, Integration integration, Conversation? convo)
    {
        Assert.True(env.ConfiguredForSnapshot);

        if (convo is not null)
        {
            await env.ReloadAsync(convo.Links.ToArray());
            await env.ReloadAsync(convo.Events.ToArray());
        }

        env.Integrations.ReadSettings<FakeSettings>(integration);
        return new {
            // Preserving the old snapshot format for simplicity
            target = new {
                convo?.Links,
                convo?.Events,
                env.BackgroundSlackClient.EnqueueDirectMessagesCalls,
                env.BusTestHarness,
                env.SignalHandler.RaisedSignals,
                env.AnalyticsClient,
            },
            logs = env.GetAllLogs(),
        };
    }

    [UsesVerify]
    public class TheLinkConversationToTicketAsyncMethod
    {
        FakeTicketLinker _ticketLinker = null!;

        TestEnvironmentWithData CreateTestEnvironment()
        {
            var dispatcher = new FakeMessageDispatcherWrapper
            {
                MessageIdToReturn = "1683117014.100249"
            };
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<ITicketLinker<FakeSettings>, FakeTicketLinker>(ServiceLifetime.Transient)
                .ReplaceService<IMessageDispatcher>(dispatcher)
                .Build(snapshot: true);
            _ticketLinker = env.Get<ITicketLinker<FakeSettings>>().Require<FakeTicketLinker>();
            env.Integrations.AddSettingsType<FakeSettings>();
            return env;
        }

        [Fact]
        public async Task DoesNothingIfOrganizationIdDoesNotExist()
        {
            var env = CreateTestEnvironment();
            var integration = await env.CreateIntegrationAsync(TestIntegrationType, enabled: true);
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                new Id<Organization>(42),
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties);

            Assert.Empty(_ticketLinker.Tickets);
            Assert.Empty(env.BackgroundSlackClient.EnqueueDirectMessagesCalls);
        }

        [Fact]
        public async Task DoesNothingIfIntegrationIdDoesNotExist()
        {
            var env = CreateTestEnvironment();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                new Id<Integration>(246),
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties);

            Assert.Empty(_ticketLinker.Tickets);
            Assert.Empty(env.BackgroundSlackClient.EnqueueDirectMessagesCalls);
        }

        [Fact]
        public async Task DoesNothingIfActorDoesNotExist()
        {
            var env = CreateTestEnvironment();
            var integration = await env.CreateIntegrationAsync(TestIntegrationType, enabled: true);
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                new Id<Member>(42),
                env.TestData.Organization,
                TestProperties);

            Assert.Empty(_ticketLinker.Tickets);
            Assert.Empty(env.BackgroundSlackClient.EnqueueDirectMessagesCalls);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember)]
        [InlineData(TestMemberType.ForeignMember)]
        [InlineData(TestMemberType.HomeGuest)]
        public async Task ReportErrorIfConversationDoesNotExist(TestMemberType actorType)
        {
            // Start an activity so we can see it in the error reporting.
            using var act = new Activity("Test");
            act.Start();

            var env = CreateTestEnvironment();
            var integration = await env.CreateIntegrationAsync(TestIntegrationType, enabled: true);
            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            var actor = env.TestData.GetMember(actorType);
            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                new Id<Conversation>(42),
                new Uri("https://example.com"),
                actor,
                actor.Organization,
                TestProperties);

            Assert.Empty(_ticketLinker.Tickets);
            await Verify(env, integration, null).UseParameters(actorType);
        }

        [Fact]
        public async Task ReportErrorIfOrgNoLongerHasConvoTracking()
        {
            var env = CreateTestEnvironment();
            env.TestData.Organization.PlanType = PlanType.Free;
            await env.Db.SaveChangesAsync();

            var integration = await env.CreateIntegrationAsync(TestIntegrationType, enabled: true);
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties);

            Assert.Empty(_ticketLinker.Tickets);
            await Verify(env, integration, convo);
        }

        [Fact]
        public async Task ReportErrorIfConversationAlreadyLinked()
        {
            var env = CreateTestEnvironment();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);

            var integration = await env.CreateIntegrationAsync(TestIntegrationType, enabled: true);

            await env.CreateConversationLinkAsync(
                convo,
                TestConversationLinkType,
                TestTicketLink.ApiUrl.ToString());

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties);

            Assert.Empty(_ticketLinker.Tickets);
            await Verify(env, integration, convo);
        }

        [Fact]
        public async Task ReportErrorIfOrgDoesNotHaveIntegrationEnabled()
        {
            var env = CreateTestEnvironment();

            var integration = await env.CreateIntegrationAsync(TestSettings, enabled: false);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties);

            Assert.Empty(_ticketLinker.Tickets);
            await Verify(env, integration, convo);
        }

        [Fact]
        public async Task ReportErrorIfIntegrationDoesNotHaveCredentials()
        {
            var env = CreateTestEnvironment();

            var integration = await env.CreateIntegrationAsync(
                new FakeSettings { HasApiCredentials = false },
                enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties);

            Assert.Empty(_ticketLinker.Tickets);
            await Verify(env, integration, convo);
        }

        [Fact]
        public async Task ReportErrorIfCredentialsInvalid()
        {
            var env = CreateTestEnvironment();
            var integration = await env.CreateIntegrationAsync(TestSettings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties.With("error",
                    (object?)_ticketLinker.CreateApiExceptionAsync(env, HttpStatusCode.Unauthorized)
                ));
            await Verify(env, integration, convo);
        }

        [Fact]
        public async Task ReportErrorIfCreateFailsWithApiException()
        {
            var env = CreateTestEnvironment();
            var integration = await env.CreateIntegrationAsync(TestSettings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties.With("error",
                    (object?)_ticketLinker.CreateApiExceptionAsync(env, HttpStatusCode.BadRequest)
                ));

            await Verify(env, integration, convo);
        }

        [Fact]
        public async Task ReportErrorIfCreateFailsForOtherReason()
        {
            var env = CreateTestEnvironment();
            var integration = await env.CreateIntegrationAsync(TestSettings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                TestProperties.With("error", (object?)new Exception("boom")));

            await Verify(env, integration, convo);
        }

        [Fact]
        public async Task ReportErrorIfCreateFailsWithTicketConfigurationException()
        {
            var env = CreateTestEnvironment();
            var integration = await env.CreateIntegrationAsync(TestSettings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                env.TestData.Member,
                env.TestData.Member.Organization,
                new Dictionary<string, object?>()
                {
                    ["title"] = "Subject",
                    ["body"] = "Description",
                    ["error"] = new TicketConfigurationException("Integration is misconfigured!"),
                });

            Assert.Empty(_ticketLinker.Tickets);
            await Verify(env, integration, convo);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember)]
        [InlineData(TestMemberType.ForeignMember)]
        [InlineData(TestMemberType.HomeGuest)]
        public async Task ReportPendingTicketCreation(TestMemberType actorType)
        {
            var env = CreateTestEnvironment();

            var actor = env.TestData.GetMember(actorType);
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                actor,
                actor.Organization,
                TestProperties
                    .With("no-link", (object?)"pending"));

            await Verify(env, integration, convo).UseParameters(actorType);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember)]
        [InlineData(TestMemberType.ForeignMember)]
        [InlineData(TestMemberType.HomeGuest)]
        public async Task ReportErrorIfNoLinkCreatedButNotSureWhy(TestMemberType actorType)
        {
            var env = CreateTestEnvironment();

            var actor = env.TestData.GetMember(actorType);
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, enabled: true);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var convo = await env.CreateConversationAsync(room);

            var linkerJob = env.Activate<TicketLinkerJob<FakeSettings>>();

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                actor,
                actor.Organization,
                TestProperties
                    .With("no-link", (object?)"no pending link"));

            await Verify(env, integration, convo).UseParameters(actorType);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(TestMemberType.ForeignMember, RoomFlags.Default)]
        [InlineData(TestMemberType.HomeGuest, RoomFlags.Default)]
        public async Task ReportSuccessfulTicketCreationAndRaisesSignal(TestMemberType actorType, RoomFlags roomFlags)
        {
            var env = CreateTestEnvironment();
            var room = await env.CreateRoomAsync(roomFlags);
            var convo = await env.CreateConversationAsync(room);
            var actor = env.TestData.GetMember(actorType);

            var integration = await RunSuccessfulLinkAsync(env, convo, actor);

            await Verify(env, integration, convo).UseParameters(actorType, roomFlags);
        }

        [Fact]
        public async Task RepliesInThreadForZendeskWhenTicketLinked()
        {
            var env = CreateTestEnvironment();
            var room = await env.CreateRoomAsync(RoomFlags.Default);
            var convo = await env.CreateConversationAsync(room);
            var actor = env.TestData.GetMember(TestMemberType.ForeignMember);

            await RunSuccessfulLinkAsync(env, convo, actor);

            var dispatched = ((FakeMessageDispatcherWrapper)env.Get<IMessageDispatcher>()).DispatchedMessage;
            Assert.NotNull(dispatched);
            var sent = dispatched.Message;
            Assert.Equal(env.TestData.Organization.Id, dispatched.Organization.Id);
            Assert.Equal($"`[Opened]` <https://example.com/tickets/num|98989 #num>\n:bust_in_silhouette: Requested by {actor.ToMention()}", sent.Text);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default, 0)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default, 1)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(TestMemberType.ForeignMember, RoomFlags.Default)]
        [InlineData(TestMemberType.HomeGuest, RoomFlags.Default)]
        public async Task ReportSuccessfulTicketCreationToAssigneesAndNonSupporteeActor(TestMemberType actorType, RoomFlags roomFlags, int? actorIndex = null)
        {
            var env = CreateTestEnvironment();

            await env.AssignDefaultFirstResponderAsync(
                await env.CreateMemberInAgentRoleAsync("UorgFR"));
            var room = await env.CreateRoomAsync(roomFlags,
                firstResponders: new[] { await env.CreateMemberInAgentRoleAsync("UroomFR") });
            var convo = await env.CreateConversationAsync(room);
            convo.Assignees.AddRange(new[]
            {
                await env.CreateMemberInAgentRoleAsync("UconvoAssign1"),
                await env.CreateMemberInAgentRoleAsync("UconvoAssign2"),
            });

            var actor = actorIndex != null
                ? convo.Assignees[actorIndex.Value]
                : env.TestData.GetMember(actorType);
            var integration = await RunSuccessfulLinkAsync(env, convo, actor);

            await Verify(env, integration, convo).UseParameters(actorType, roomFlags, actorIndex);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default, 0)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default, 1)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(TestMemberType.ForeignMember, RoomFlags.Default)]
        [InlineData(TestMemberType.HomeGuest, RoomFlags.Default)]
        public async Task ReportSuccessfulTicketCreationToRoomFirstRespondersAndNonSupporteeActor(TestMemberType actorType, RoomFlags roomFlags, int? actorIndex = null)
        {
            var env = CreateTestEnvironment();

            await env.AssignDefaultFirstResponderAsync(
                await env.CreateMemberInAgentRoleAsync("UorgFR"));
            var firstResponders = new[]
            {
                await env.CreateMemberInAgentRoleAsync("UroomFR1"),
                await env.CreateMemberInAgentRoleAsync("UroomFR2"),
            };
            var room = await env.CreateRoomAsync(roomFlags, firstResponders: firstResponders);
            var convo = await env.CreateConversationAsync(room);

            var actor = actorIndex != null
                ? firstResponders[actorIndex.Value]
                : env.TestData.GetMember(actorType);
            var integration = await RunSuccessfulLinkAsync(env, convo, actor);

            await Verify(env, integration, convo).UseParameters(actorType, roomFlags, actorIndex);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default, 0)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.Default, 1)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(TestMemberType.ForeignMember, RoomFlags.Default)]
        [InlineData(TestMemberType.HomeGuest, RoomFlags.Default)]
        public async Task ReportSuccessfulTicketCreationToDefaultFirstRespondersAndNonSupporteeActor(TestMemberType actorType, RoomFlags roomFlags, int? actorIndex = null)
        {
            var env = CreateTestEnvironment();

            var defaultFirstResponders = new[] {
                await env.AssignDefaultFirstResponderAsync(
                    await env.CreateMemberInAgentRoleAsync("UorgFR1")),
                await env.AssignDefaultFirstResponderAsync(
                    await env.CreateMemberInAgentRoleAsync("UorgFR2")),
            };
            var room = await env.CreateRoomAsync(roomFlags,
                escalationResponders: new[]
                {
                    await env.CreateMemberInAgentRoleAsync("UroomER1"), // Ignored
                });
            var convo = await env.CreateConversationAsync(room);

            var actor = actorIndex != null
                ? defaultFirstResponders[actorIndex.Value]
                : env.TestData.GetMember(actorType);
            var integration = await RunSuccessfulLinkAsync(env, convo, actor);

            await Verify(env, integration, convo).UseParameters(actorType, roomFlags, actorIndex);
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember)]
        [InlineData(TestMemberType.ForeignMember)]
        [InlineData(TestMemberType.HomeGuest)]
        public async Task ReportSuccessfulTicketCreationWhenConversationManagementNotEnabledInRoom(TestMemberType actorType)
        {
            var env = CreateTestEnvironment();

            var room = await env.CreateRoomAsync(managedConversationsEnabled: false);
            var convo = await env.CreateConversationAsync(room, initialState: ConversationState.Hidden);

            var actor = env.TestData.GetMember(TestMemberType.HomeMember);
            var integration = await RunSuccessfulLinkAsync(env, convo, actor);

            await Verify(env, integration, convo).UseParameters(actorType);
        }

        static async Task<Integration> RunSuccessfulLinkAsync(
            TestEnvironmentWithData env,
            Conversation convo,
            Member actor)
        {
            var settings = TestSettings;
            var integration = await env.CreateIntegrationAsync(settings, externalId: "num", enabled: true);

            // Use clean DbContext for job
            using var jobScope = env.ScopeFactory.CreateScope();
            jobScope.ServiceProvider.GetRequiredService<IIntegrationRepository>()
                .Require<FakeIntegrationRepository>()
                .AddSettingsType<FakeSettings>();
            var linkerJob = jobScope.ServiceProvider.Activate<TicketLinkerJob<FakeSettings>>();

            var fields = new Dictionary<string, object?>()
            {
                ["title"] = $"Title",
                ["body"] = "Description",
            };

            await linkerJob.LinkConversationToTicketAsync(
                env.TestData.Organization,
                integration,
                convo,
                new Uri("https://example.com"),
                actor,
                actor.Organization,
                fields);

            return integration;
        }
    }

    public class FakeSettings : IIntegrationSettings, ITicketingSettings
    {
        public static IntegrationType IntegrationType => TestIntegrationType;
        string ITicketingSettings.IntegrationName => "Fake Ticketing";
        string ITicketingSettings.IntegrationSlug => "fake-ticketing";
        ConversationLinkType ITicketingSettings.ConversationLinkType => TestConversationLinkType;

        public IntegrationLink? GetTicketLink(ConversationLink? conversationLink) =>
            conversationLink is { }
                ? new FakeTicketLink(conversationLink.ExternalId)
                : null;

        public required bool HasApiCredentials { get; init; }
    }

    public record FakeTicketLink : IntegrationLink
    {
        public FakeTicketLink(string ticketNumber, bool hasWebUrl = true)
        {
            ApiUrl = new Uri($"https://api.example.com/tickets/{ticketNumber}");
            WebUrl = hasWebUrl ? new Uri($"https://example.com/tickets/{ticketNumber}") : null;
        }

        public override IntegrationType IntegrationType => TestIntegrationType;
        public override Uri ApiUrl { get; }
        public override Uri? WebUrl { get; }
    }

    public class FakeTicketLinker : ITicketLinker<FakeSettings>
    {
        readonly IUrlGenerator _urlGenerator;
        readonly HashSet<Conversation> _pendingConversations = new();

        public FakeTicketLinker(IUrlGenerator urlGenerator)
        {
            _urlGenerator = urlGenerator;
        }

        public List<FakeTicket> Tickets { get; } = new List<FakeTicket>();

        async Task<ConversationLink?> ITicketLinker<FakeSettings>.CreateTicketLinkAsync(
            Integration integration,
            FakeSettings settings,
            IReadOnlyDictionary<string, object?> properties,
            Conversation conversation,
            Member actor)
        {
            Assert.True(integration.Organization.HasPlanFeature(PlanFeature.ConversationTracking));
            Assert.True(integration.Enabled);
            Assert.True(settings.HasApiCredentials);

            if (properties.TryGetValue("error", out var error) && error is Exception ex)
            {
                throw ex;
            }

            if (properties.TryGetValue("no-link", out var noLink))
            {
                // Only pending returns a URL from GetPendingUrl()
                if (noLink is "pending")
                {
                    _pendingConversations.Add(conversation);
                }
                return null;
            }

            var link = new ConversationLink
            {
                Conversation = conversation,
                Organization = conversation.Organization,
                CreatedBy = actor,
                LinkType = TestConversationLinkType,
                ExternalId = "num",
            };
            Tickets.Add(new FakeTicket(link, properties));
            return link;
        }

        public Task<ApiException> CreateApiExceptionAsync(TestEnvironment env,
            HttpStatusCode statusCode) =>
            env.CreateApiExceptionAsync(statusCode, HttpMethod.Post, "https://example.com/tickets",
                statusCode switch
                {
                    HttpStatusCode.Unauthorized => new { error = "Unauthorized" },
                    _ => new { foo = "bar" },
                });

        public string? GetPendingTicketUrl(Conversation conversation, Member actor) =>
            _pendingConversations.Contains(conversation)
                ? _urlGenerator.PendingTicketPage(conversation, TestIntegrationType, actor).ToString()
                : null;

        public record FakeTicket(
            ConversationLink Link,
            IReadOnlyDictionary<string, object?> Properties);
    }
}
