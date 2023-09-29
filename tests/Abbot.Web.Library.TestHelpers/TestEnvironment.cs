using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;
using Abbot.Common.TestHelpers.Fakes;
using Hangfire;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using NSubstitute;
using Refit;
using Segment;
using Serious;
using Serious.Abbot.AI;
using Serious.Abbot.Clients;
using Serious.Abbot.Compilation;
using Serious.Abbot.Configuration;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.StateMachines;
using Serious.Abbot.Events;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Compilation;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Live;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Serialization;
using Serious.Abbot.Services;
using Serious.Abbot.Signals;
using Serious.Abbot.Skills;
using Serious.Abbot.Storage.FileShare;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Serious.TestHelpers;
using Xunit.Abstractions;
using ConversationState = Serious.Abbot.Entities.ConversationState;

namespace Abbot.Common.TestHelpers;

/// <summary>
/// A <see cref="TestEnvironment"/> is a fully constitutes set of fake services suitable for testing any component.
/// A test can create a test environment and then use that environment to activate any System Under Test (SUT) from that environment.
/// </summary>
public class TestEnvironment
{
    /// <summary>
    /// Returns <see langword="true"/> if this <see cref="TestEnvironment"/>
    /// is configured for snapshot stability.
    /// Set by <see cref="ConfigureForSnapshot"/>.
    /// </summary>
    public bool ConfiguredForSnapshot { get; private set; }

    public static readonly DateTime TestOClock =
        new(1999, 01, 02, 03, 04, 05, 0, DateTimeKind.Utc);

    /// <summary>
    /// Configures this environment for snapshot stability.
    /// <list type="bullet">
    ///   <item>Sets <see cref="ConfigureForSnapshot"/> to <see langword="true"/>.</item>
    ///   <item>Sets <see cref="FakeAuditLog.AdvanceByOnSave"/> to 1 ms.</item>
    /// </list>
    /// </summary>
    public void ConfigureForSnapshot()
    {
        ConfiguredForSnapshot = true;
        Expect.True(Clock.UtcNow == TestOClock);
        AuditLog.AdvanceByOnSave = TimeSpan.FromMilliseconds(1);
    }

    /// <summary>
    /// Gets an <see cref="IServiceProvider"/> that grants access to the root scope-less service provider.
    /// </summary>
    public IServiceProvider RootServices { get; }

    /// <summary>
    /// Gets an <see cref="IServiceScopeFactory"/> that can be used to construct new service scopes.
    /// </summary>
    public IServiceScopeFactory ScopeFactory { get; }

    /// <summary>
    /// Gets the default <see cref="IServiceScope"/> for the test.
    /// </summary>
    public IServiceScope DefaultScope { get; }

    /// <summary>
    /// Gets the default <see cref="IServiceProvider" /> to use to access services.
    /// This is the service provider associated with the <see cref="DefaultScope"/>.
    /// </summary>
    public IServiceProvider Services => DefaultScope.ServiceProvider;

    public IdGenerator IdGenerator => Get<IdGenerator>();

    [Obsolete("Only for DI purposes")]
    public TestEnvironment(IServiceProvider services, IServiceScopeFactory scopeFactory)
    {
        RootServices = services;
        ScopeFactory = scopeFactory;

        // Create a default scope
        DefaultScope = scopeFactory.CreateScope();

        // Skeeeeetchy, but it works until it doesn't!
        foreach (var service in services.GetRequiredService<IEnumerable<IHostedService>>())
        {
            service.StartAsync(default).Wait(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>
    /// Creates a <see cref="TestEnvironment"/> to execute tests in.
    /// The environment will have no test data created.
    /// </summary>
    /// <param name="snapshot">Configure <see cref="TestEnvironment"/> for snapshot stability.</param>
    /// <returns>The constructed <see cref="TestEnvironment"/></returns>
    public static TestEnvironment CreateWithoutData(bool snapshot = false) =>
        TestEnvironmentBuilder.CreateWithoutData().Build(snapshot);

    /// <summary>
    /// Creates a <see cref="TestEnvironmentWithData"/> to execute tests in.
    /// The environment will contain test data.
    /// </summary>
    /// <param name="snapshot">Configure <see cref="TestEnvironment"/> for snapshot stability.</param>
    /// <returns>The constructed <see cref="TestEnvironmentWithData"/></returns>
    public static TestEnvironmentWithData Create(bool snapshot = false) =>
        TestEnvironmentBuilder.Create().Build(snapshot);

    /// <summary>
    /// Creates a <see cref="TestEnvironmentWithData{TData}"/> to execute tests in.
    /// The environment will contain test data in the form of an instance of <typeparamref name="TData"/>.
    /// </summary>
    /// <typeparam name="TData">A type deriving from <see cref="CommonTestData"/> which seeds additional test data for this test.</typeparam>
    /// <param name="snapshot">Configure <see cref="TestEnvironment"/> for snapshot stability.</param>
    /// <returns>The constructed <see cref="TestEnvironmentWithData{TData}"/>.</returns>
    public static TestEnvironmentWithData<TData> Create<TData>(bool snapshot = false)
        where TData : CommonTestData =>
        TestEnvironmentBuilder.Create<TData>().Build(snapshot);

    public FakeAbbotContext Db => (FakeAbbotContext)Get<AbbotContext>();

    public FakeDataProtectionProvider DataProtectionProvider => (FakeDataProtectionProvider)Get<IDataProtectionProvider>();

    public FakeAssemblyCache AssemblyCache => (FakeAssemblyCache)Get<IAssemblyCache>();

    public FakeAssemblyCacheClient AssemblyCacheClient => (FakeAssemblyCacheClient)Get<IAssemblyCacheClient>();

    public FakeAuthenticationService Authentication => (FakeAuthenticationService)Get<IAuthenticationService>();

    public FakeAzureKeyVaultClient AzureKeyVaultClient => (FakeAzureKeyVaultClient)Get<IAzureKeyVaultClient>();

    public FakeSimpleSlackApiClient SlackApi => (FakeSimpleSlackApiClient)Get<ISlackApiClient>();

    public FakeGeocodingService GeocodingApi => (FakeGeocodingService)Get<IGeocodeService>();

    public FakeBackgroundSlackClient BackgroundSlackClient => (FakeBackgroundSlackClient)Get<IBackgroundSlackClient>();

    public FakeSkillRunnerClient SkillRunnerClient => (FakeSkillRunnerClient)Get<ISkillRunnerClient>();

    public IAliasRepository Aliases => Get<IAliasRepository>();

    public FakeAuditLog AuditLog => (FakeAuditLog)Get<IAuditLog>();

    public FakeSkillCompiler Compiler => (FakeSkillCompiler)Get<ISkillCompiler>();

    public FakeCachingCompilerService CachingCompilerService =>
        (FakeCachingCompilerService)Get<ICachingCompilerService>();

    public FakeSignalHandler SignalHandler => (FakeSignalHandler)Get<ISignalHandler>();

    public FakeOpenAiClient OpenAiClient => (FakeOpenAiClient)Get<IOpenAIClient>();

    public FakeResponder Responder => (FakeResponder)Get<IResponder>();

    public FakeFeatureManager Features => (FakeFeatureManager)Get<IFeatureManager>();

    public FakeBuiltinSkillRegistry BuiltinSkillRegistry => (FakeBuiltinSkillRegistry)Get<IBuiltinSkillRegistry>();

    public FakeHttpMessageHandler Http => (FakeHttpMessageHandler)Get<HttpMessageHandler>();

    public FakeBackgroundJobClient BackgroundJobClient => (FakeBackgroundJobClient)Get<IBackgroundJobClient>();

    public FakeInsightsRepository InsightsRepository => (FakeInsightsRepository)Get<IInsightsRepository>();

    public SkillOptions SkillOptions => Get<IOptions<SkillOptions>>().Value;

    public CustomerRepository Customers => Get<CustomerRepository>();
    public IRoomRepository Rooms => Get<IRoomRepository>();

    public IUserRepository Users => Get<IUserRepository>();

    public NotificationRepository Notifications => Get<NotificationRepository>();

    public IRoleManager Roles => Get<IRoleManager>();

    public ISlackThreadExporter SlackThreadExporter => Get<ISlackThreadExporter>();

    public FakeTextAnalyticsClient TextAnalyticsClient => (FakeTextAnalyticsClient)Get<ITextAnalyticsClient>();

    public ILinkedIdentityRepository LinkedIdentities => Get<ILinkedIdentityRepository>();
    public IListRepository Lists => Get<IListRepository>();

    public IOrganizationRepository Organizations => Get<IOrganizationRepository>();

    public PlaybookRepository Playbooks => Get<PlaybookRepository>();
    public PlaybookPublisher PlaybookPublisher => Get<PlaybookPublisher>();

    public IPackageRepository Packages => Get<IPackageRepository>();

    public AISettingsRegistry AISettings => Get<AISettingsRegistry>();
    public ISettingsManager Settings => Get<ISettingsManager>();
    public ISkillRepository Skills => Get<ISkillRepository>();
    public IPatternRepository Patterns => Get<IPatternRepository>();

    public ISkillSecretRepository SkillSecrets => Get<ISkillSecretRepository>();

    public ITriggerRepository Triggers => Get<ITriggerRepository>();

    public IMetadataRepository Metadata => Get<IMetadataRepository>();

    public ITagRepository Tags => Get<ITagRepository>();

    public FakeConversationRepository Conversations => (FakeConversationRepository)Get<IConversationRepository>();

    public IHubRepository Hubs => Get<IHubRepository>();

    public IFormsRepository Forms => Get<IFormsRepository>();

    public FakeConversationPublisher ConversationPublisher => (FakeConversationPublisher)Get<IConversationPublisher>();

    public FakeConversationTracker ConversationTracker => (FakeConversationTracker)Get<IConversationTracker>();

    public FakeConversationListener ConversationListener => Get<IEnumerable<IConversationListener>>().OfType<FakeConversationListener>().Single();

    public FakeAnalyticsClient AnalyticsClient => (FakeAnalyticsClient)Get<IAnalyticsClient>();

    public IMemoryRepository Memories => Get<IMemoryRepository>();

    public IPermissionRepository Permissions => Get<IPermissionRepository>();

    public TimeTravelClock Clock => (TimeTravelClock)Get<IClock>();

    public FakeStopwatchFactory StopwatchFactory => (FakeStopwatchFactory)Get<IStopwatchFactory>();

    public FakeRouter Router => (FakeRouter)Get<IRouter>();

    public FakeIntegrationRepository Integrations => (FakeIntegrationRepository)Get<IIntegrationRepository>();

    [return: NotNullIfNotNull("value")]
    public SecretString? Secret(string? value) =>
        value == null ? null : new SecretString(value, Get<IDataProtectionProvider>());

    public FakeZendeskClientFactory ZendeskClientFactory =>
        (FakeZendeskClientFactory)Get<IZendeskClientFactory>();

    public FakeSlackToZendeskCommentImporter SlackToZendeskCommentImporter =>
        (FakeSlackToZendeskCommentImporter)Get<ISlackToZendeskCommentImporter>();

    public FakeHubSpotClientFactory HubSpotClientFactory =>
        (FakeHubSpotClientFactory)Get<IHubSpotClientFactory>();

    public IHubSpotLinker HubSpotLinker => Get<IHubSpotLinker>();

    public FakeGitHubClientFactory GitHubClientFactory =>
        (FakeGitHubClientFactory)Get<IGitHubClientFactory>();

    public FakeMergeDevClientFactory MergeDevClientFactory =>
        (FakeMergeDevClientFactory)Get<IMergeDevClientFactory>();

    public FakeHostEnvironment HostEnvironment => (FakeHostEnvironment)Get<IHostEnvironment>();

    public FakeLoggerProvider LoggerProvider => (FakeLoggerProvider)Get<ILoggerProvider>();

    public FakeBotFrameworkAdapter BotFrameworkAdapter => (FakeBotFrameworkAdapter)Get<IBotFrameworkAdapter>();

    public FakeRecurringJobManager RecurringJobManager => (FakeRecurringJobManager)Get<IRecurringJobManager>();

    public IScheduledSkillClient ScheduledSkillClient => Get<IScheduledSkillClient>();

    public ITestHarness BusTestHarness => Get<ITestHarness>();

    public ISagaStateMachineTestHarness<PlaybookRunStateMachine, PlaybookRun> BusPlaybookSagaStateMachineHarness =>
        BusTestHarness.GetSagaStateMachineHarness<PlaybookRunStateMachine, PlaybookRun>();

    /// <summary>
    /// Waits for a <paramref name="run"/> saga to reach the <c>Final</c> state.
    /// </summary>
    /// <param name="run">The <see cref="PlaybookRun"/>.</param>
    public async Task WaitForPlaybookRunAsync(PlaybookRun run) =>
        Assert.NotNull(await BusPlaybookSagaStateMachineHarness.Exists(run.CorrelationId, sm => sm.Final));

    public ConsumerTestObserver ConsumerObserver => Get<ConsumerTestObserver>();

    public FakeFlashPublisher FlashPublisher => (FakeFlashPublisher)Get<IFlashPublisher>();

    /// <summary>
    /// Activates a new instance of <typeparamref name="T" /> using the services registered in the container.
    /// </summary>
    /// <typeparam name="T">The type to activate.</typeparam>
    public T Activate<T>() => Services.Activate<T>();

    /// <summary>
    /// Activates a new instance of <typeparamref name="T"/> the services registered in the container,
    /// in an isolated <see cref="IServiceScope"/>.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">The activated service.</param>
    /// <returns>The new <see cref="IServiceScope"/>.</returns>
    public IServiceScope ActivateInNewScope<T>(out T service)
    {
        var scope = ScopeFactory.CreateScope();
        service = scope.ServiceProvider.Activate<T>();
        return scope;
    }

    /// <summary>
    /// Gets a required service registered in the container, in an isolated <see cref="IServiceScope"/>.
    /// </summary>
    /// <remarks>
    /// Similar to <see cref="ActivateInNewScope{T}"/>, but doesn't create a new instance (unless the service is
    /// registered with a Transient lifetime).
    /// </remarks>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">The activated service.</param>
    /// <returns>The new <see cref="IServiceScope"/>.</returns>
    public IServiceScope GetRequiredServiceInNewScope<T>(out T service) where T : notnull
    {
        var scope = ScopeFactory.CreateScope();
        service = scope.ServiceProvider.GetRequiredService<T>();
        return scope;
    }

    /// <summary>
    /// Gets an instance of <typeparamref name="T" /> from the container, throwing if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type to retrieve from the container.</typeparam>
    /// <returns>An instance of <typeparamref name="T" /> activated from the container.</returns>
    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>
    /// Reloads the specified entities with the latest values from the database.
    /// </summary>
    /// <param name="entities">The entities to reload.</param>
    public async Task ReloadAsync(params object?[] entities)
    {
        foreach (var entity in entities.WhereNotNull())
        {
            var entry = Db.Entry(entity);
            if (entry.State == EntityState.Added)
            {
                throw new InvalidOperationException($"{entity.GetType().Name} was never saved!");
            }
            await entry.ReloadAsync();
        }
    }

    public async Task<ApiException> CreateApiExceptionAsync(
        HttpStatusCode statusCode,
        HttpMethod method,
        string uri = "",
        object? payload = null)
    {
        var json = JsonConvert.SerializeObject(payload ?? new { });
        var req = new HttpRequestMessage(method, uri);
        var resp = new HttpResponseMessage(statusCode);
        resp.Content = new StringContent(json);
        return await ApiException.Create(req, req.Method, resp, new RefitSettings());
    }

    /// <summary>
    /// Reloads the specified entities with the latest values from the database.
    /// </summary>
    /// <param name="entities">The entities to reload.</param>
    public async Task ReloadAsync<T>(IEnumerable<T> entities) => await ReloadAsync(entities.ToArray());

    /// <summary>
    /// Reloads the specified entities with the latest values from the database.
    /// </summary>
    /// <param name="entities">The entities to reload.</param>
    public async Task ReloadAsync<T>(params T[] entities) => await ReloadAsync(entities.Cast<object>().ToArray());

    /// <summary>
    /// Runs <paramref name="updater"/> on the entity and then immediately marks it as modified and saves changes to the DB.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="updater">A function that modifies the entity.</param>
    public async Task UpdateAsync<T>(T entity, Action<T> updater) where T : class
    {
        updater(entity);
        Db.Entry(entity).State = EntityState.Modified;
        await Db.SaveChangesAsync();

        // Shouldn't be necessary, but always good to go back to the DB to be safe when testing.
        await ReloadAsync(entity);
    }

    public async Task<Organization> CreateOrganizationAsync(
        string? platformId = null,
        PlanType plan = PlanType.Free,
        string? name = null,
        string? domain = null,
        string? slug = null,
        string? botId = null,
        string? botUserId = null,
        string? botName = null,
        string? avatar = null,
        string? scopes = null,
        string? apiToken = null,
        int purchasedSeats = 0,
        TrialPlan? trialPlan = null,
        bool enabled = true)
    {
        var orgNumber = IdGenerator.GetId();

        var org = await Organizations.CreateOrganizationAsync(
            platformId ?? IdGenerator.GetSlackTeamId(orgNumber),
            plan,
            name ?? $"Test Organization {orgNumber}",
            domain ?? $"testorg{orgNumber}.example.com",
            slug ?? $"test-org-{orgNumber}",
            avatar ?? $"https://avatars.slack-edge.com/org-{orgNumber}.png");

        org.PurchasedSeatCount = purchasedSeats;
        org.Trial = trialPlan;
        org.Enabled = enabled;

        var auth = CreateSlackAuthorization("A01234", "Test Abbot", botId, botUserId, botName, scopes, apiToken, orgNumber);
        auth.Apply(org);
        await Db.SaveChangesAsync();
        return org;
    }

    public SlackAuthorization CreateSlackAuthorization(
        string? appId,
        string? appName = null,
        string? botId = null,
        string? botUserId = null,
        string? botName = null,
        string? scopes = null,
        string? apiToken = null,
        int? orgNumber = null) =>
        new(appId,
            appName ?? (orgNumber is null ? "Test Abbot" : $"Test Abbot {orgNumber}"),
            botId ?? IdGenerator.GetSlackBotId(orgNumber),
            botUserId ?? IdGenerator.GetSlackUserId(orgNumber),
            botName ?? (orgNumber is null ? "test-abbot" : $"test-abbot-{orgNumber}"),
            $"https://example.com/bot-avatar{(orgNumber is null ? "" : "-" + orgNumber)}.png",
            BotResponseAvatar: null,
            Secret(apiToken ?? $"xoxb-this-is-a-test{(orgNumber is null ? "" : "-" + orgNumber)}-token"),
            scopes ?? CommonTestData.DefaultOrganizationScopes);

    /// <summary>
    /// Fetches all logs from the logger category defined by <typeparamref name="T"/>.
    /// </summary>
    /// <param name="minimumLevel">The minimum <see cref="LogLevel"/> of events to retrieve.</param>
    /// <param name="eventName">If specified, only events matching this name will be retrieved.</param>
    /// <typeparam name="T">The .NET type that represents the category to fetch.</typeparam>
    public IReadOnlyList<LogEvent> GetAllLogs<T>(LogLevel? minimumLevel = null, string? eventName = null) =>
        GetAllLogs(FakeLoggerProvider.GetCategoryName<T>(), minimumLevel);

    /// <summary>
    /// Fetches all logs from categories that match the category prefix specified by <paramref name="categoryPrefix"/>.
    /// </summary>
    /// <param name="categoryPrefix">
    /// The category prefix to match.
    /// The default value if this is not specified is "Serious.".
    /// Specify 'null' explicitly to match all categories.
    /// </param>
    /// <param name="minimumLevel">The minimum <see cref="LogLevel"/> of events to retrieve.</param>
    /// <param name="eventName">If specified, only events matching this name will be retrieved.</param>
    /// <typeparam name="T">The .NET type that represents the category to fetch.</typeparam>
    public IReadOnlyList<LogEvent> GetAllLogs(string? categoryPrefix = "Serious.", LogLevel? minimumLevel = null, string? eventName = null) =>
        LoggerProvider.GetAllEvents(categoryPrefix, minimumLevel, eventName);
}

public class TestEnvironmentWithData : TestEnvironment
{
    public CommonTestData TestData { get; }

    [Obsolete("Only for DI or inheritance purposes")]
    public TestEnvironmentWithData(IServiceProvider services, IServiceScopeFactory scopeFactory, CommonTestData testData) : base(services, scopeFactory)
    {
        TestData = testData;

        var t = testData.InitializeAsync(this);
        if (!t.IsCompleted)
        {
            throw new InvalidOperationException("Seeding actually went async!?");
        }

        // Manifest any exceptions
        t.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Activates a new instance of <typeparamref name="T" /> using the services registered in the container.
    /// If the type represents a Razor page or Controller, the instance is activated with an appropriate HttpContext.
    /// </summary>
    /// <typeparam name="T">The type to activate.</typeparam>
    public T Activate<T>(RegistrationStatus registrationStatus = RegistrationStatus.Ok, Member? loggedInMember = null,
        PageContext? pageContext = null, ControllerContext? controllerContext = null)
    {
        var instance = base.Activate<T>();
        if (instance is ControllerBase or PageModel)
        {
            var member = loggedInMember ?? TestData.Member;
            var httpContext = new FakeHttpContext
            {
                User = new FakeClaimsPrincipal(member, registrationStatus),
                RequestServices = Services
            };
            httpContext.SetCurrentMember(member);

            var actionDescriptor = new CompiledPageActionDescriptor
            {
                EndpointMetadata = new List<object>()
            };

            switch (instance)
            {
                case PageModel page:
                    page.PageContext =
                        pageContext ?? new PageContext(new FakeActionContext(httpContext,
                            new RouteData(),
                            actionDescriptor,
                            Router));

                    break;

                case ControllerBase controller:
                    controller.ControllerContext = controllerContext ?? new FakeControllerContext(httpContext, Router);
                    break;
            }
        }

        return instance;
    }

    public IPlatformEvent<TPayload> CreateFakePlatformEvent<TPayload>(TPayload payload, Member? from = null, Room? room = null)
    {
        var organization = TestData.Organization;
        return new PlatformEvent<TPayload>(
            payload,
            null,
            BotChannelUser.GetBotUser(organization),
            Clock.UtcNow,
            Responder,
            from ?? TestData.Member,
            room,
            organization);
    }

    public ITurnContext CreateTurnContextForMessageEvent(
        string text,
        string channel,
        string? ts = null,
        string? threadTs = null,
        Member? from = null,
        Organization? organization = null)
    {
        var messageEvent = CreateMessageEventEnvelope(text, channel, ts, threadTs, from: from, organization: organization);
        return CreateTurnContext("message", messageEvent);
    }

    public ITurnContext CreateTurnContext(string type, object channelData)
    {
        return new FakeTurnContext(new Activity
        {
            Type = type,
            Text = channelData is IEventEnvelope<MessageEvent> messageEvent ? messageEvent.Event.Text : "null",
            ChannelData = channelData
        });
    }

    public IEventEnvelope<MessageEvent> CreateMessageEventEnvelope(
        string text,
        string channel,
        string? ts = null,
        string? threadTs = null,
        string? eventId = null,
        string? appId = null,
        Member? from = null,
        Organization? organization = null)
    {
        organization ??= TestData.Organization;
        from ??= TestData.Member;
        return new EventEnvelope<MessageEvent>
        {
            Event = new MessageEvent
            {
                Text = text,
                Channel = channel,
                Timestamp = ts ?? IdGenerator.GetSlackMessageId(),
                ThreadTimestamp = threadTs,
                ThreadTs = threadTs,
                User = from.User.PlatformUserId,
            },
            TeamId = organization.PlatformId,
            EventId = eventId ?? IdGenerator.GetSlackEventId(),
            ApiAppId = appId ?? organization.BotAppId ?? IdGenerator.GetSlackAppId(),
        };
    }

    public MessageBlockActionsPayload CreateMessageBlockActionsPayload(
        Room room,
        string? ts = null,
        string? actionId = null,
        string? blockId = null,
        Member? from = null)
    {
        from ??= TestData.Member;
        var organization = room.Organization;
        return new MessageBlockActionsPayload
        {
            Actions = new[]
            {
                new ButtonElement { ActionId = actionId ?? "", BlockId = blockId }
            },
            User = new UserIdentifier
            {
                TeamId = organization.PlatformId,
                Id = from.User.PlatformUserId,
            },
            Team = new TeamIdentifier
            {
                Id = organization.PlatformId,
                Domain = organization.Domain ?? "unknown"
            },
            Channel = new ChannelInfo { Id = room.PlatformRoomId, Name = room.Name ?? "unknown" },
            Container = new MessageContainer(ts, false, room.PlatformRoomId),
            Message = new SlackMessage
            {
                Text = "The original message",
                Timestamp = ts,
                Blocks = new ILayoutBlock[]
                {
                    new Section("Some Blocks")
                },
            },
        };
    }

    public PlatformEvent<TPayload> CreateFakePlatformRoomEvent<TPayload>(
        TPayload payload,
        Room room,
        Member? from = null)
    {
        var organization = TestData.Organization;
        return new PlatformEvent<TPayload>(
            payload,
            null,
            BotChannelUser.GetBotUser(organization),
            Clock.UtcNow,
            Responder,
            from ?? TestData.Member,
            room,
            organization);
    }

    public ConversationMessage CreateConversationMessage(
        Conversation conversation,
        DateTime? utcTimestamp = null,
        Member? from = null,
        string? text = null,
        string? messageId = null) =>
        new(
            text ?? "Message!",
            conversation.Organization,
            from ?? TestData.Member,
            conversation.Room,
            utcTimestamp ?? Clock.UtcNow,
            messageId ?? IdGenerator.GetSlackMessageId(),
            ThreadId: conversation.FirstMessageId,
            Array.Empty<ILayoutBlock>(),
            Array.Empty<FileUpload>(),
            MessageContext: null);

    public async Task<SlackEvent> CreateSlackMessageEventAsync(
        string text,
        string channel,
        string? timestamp = null,
        string? threadTimestamp = null,
        string? eventId = null,
        string? appId = null,
        Member? from = null,
        Organization? org = null)
    {
        var organization = org ?? TestData.Organization;
        var messageEvent = CreateMessageEventEnvelope(text, channel, timestamp, threadTimestamp, eventId, appId, from);
        var eventContent = JsonConvert.SerializeObject(messageEvent);
        var slackEvent = new SlackEvent
        {
            EventId = eventId ?? IdGenerator.GetSlackEventId(),
            EventType = messageEvent.Event.Type,
            Content = Secret(eventContent),
            TeamId = organization.PlatformId,
            AppId = messageEvent.ApiAppId,
        };
        await Db.SlackEvents.AddAsync(slackEvent);
        await Db.SaveChangesAsync();
        return slackEvent;
    }

    public async Task<SlackEvent> CreateSlackEventAsync(
        string? eventId = null,
        string? eventType = null,
        string? eventContent = null,
        Organization? org = null)
    {
        var organization = org ?? TestData.Organization;
        var slackEvent = new SlackEvent
        {
            EventId = eventId ?? IdGenerator.GetSlackEventId(),
            EventType = eventType ?? "message",
            Content = Secret(eventContent ?? @"{""some-content"":""stuff""}"),
            TeamId = organization.PlatformId,
            AppId = "A000000123"
        };
        await Db.SlackEvents.AddAsync(slackEvent);
        await Db.SaveChangesAsync();
        return slackEvent;
    }

    public async Task<SlackEvent> CreateSlackEventAsync<TEvent>(
        IEventEnvelope<TEvent> eventEnvelope,
        Organization? org = null) where TEvent : EventBody
    {
        var organization = org ?? TestData.Organization;
        var eventContent = JsonConvert.SerializeObject(eventEnvelope);
        return await CreateSlackEventAsync(eventContent: eventContent, org: organization);
    }

    public IViewContext<TPayload> CreateFakeViewContext<TPayload>(TPayload payload, IHandler handler, Member? from = null)
        where TPayload : IViewPayload
    {
        var platformEvent = CreateFakePlatformEvent(payload, from);
        return new ViewContext<TPayload>(platformEvent, handler);
    }

    public IPlatformMessage CreatePlatformMessageWithoutInteraction(
        Room room,
        string messageId,
        string? threadId = null,
        bool directMessage = false,
        string? message = null,
        IEnumerable<Member>? mentions = null,
        Member? from = null)
    {
        var organization = TestData.Organization;
        from ??= TestData.Member;
        var payload = new MessageEventInfo(
            message ?? "Some text",
            room.PlatformRoomId,
            from.User.PlatformUserId,
            Array.Empty<string>(),
            directMessage,
            false,
            messageId,
            threadId,
            null,
            Array.Empty<ILayoutBlock>(),
            Array.Empty<FileUpload>());
        return new PlatformMessage(
            payload,
            null,
            organization,
            Clock.UtcNow,
            Responder,
            from,
            BotChannelUser.GetBotUser(organization),
            mentions ?? Enumerable.Empty<Member>(),
            room);
    }

    public IPlatformMessage CreatePlatformMessage(
        Room? room,
        CallbackInfo? callbackInfo = null,
        Member? from = null,
        bool ephemeral = false,
        string? triggerId = null,
        Uri? messageUrl = null,
        string? messageId = null,
        string? threadId = null,
        MessageInteractionInfo? interactionInfo = null,
        MessageBlockActionsPayload? messageBlockActionsPayload = null,
        string? arguments = null,
        MessageEventInfo? payload = null,
        IEnumerable<Member>? mentions = null,
        bool directMessage = false,
        string? message = null,
        bool workflowMessage = false)
    {
        // Yeah, this is a bit of a mess. I'll clean it up later. -@haacked
        var organization = TestData.Organization;
        from ??= TestData.Member;
        messageId ??= IdGenerator.GetSlackMessageId();
        var blockActionsPayload = messageBlockActionsPayload ?? new MessageBlockActionsPayload
        {
            ResponseUrl = new Uri("https://example.com/response"),
            Container = new MessageContainer(messageId, ephemeral, ""),
            Message = new SlackMessage { Text = "Hello World!" },
        };

        if (triggerId is not null)
        {
            blockActionsPayload = blockActionsPayload with
            {
                TriggerId = triggerId
            };
        }
        callbackInfo ??= new InteractionCallbackInfo("Unknown");
        interactionInfo ??= new MessageInteractionInfo(blockActionsPayload, arguments ?? string.Empty, callbackInfo);
        payload ??= new MessageEventInfo(
            message ?? "Some text",
            room?.PlatformRoomId ?? string.Empty,
            from.User.PlatformUserId,
            Array.Empty<string>(),
            directMessage,
            false,
            messageId,
            threadId,
            interactionInfo,
            Array.Empty<ILayoutBlock>(),
            Array.Empty<FileUpload>(),
            workflowMessage);
        return new PlatformMessage(
            payload,
            messageUrl,
            organization,
            Clock.UtcNow,
            Responder,
            from,
            BotChannelUser.GetBotUser(organization),
            mentions ?? Enumerable.Empty<Member>(),
            room);
    }

    public FakeMessageContext CreateFakeMessageContext(
        string? skill = null,
        string args = "",
        Member? from = null,
        IReadOnlyList<Member>? mentions = null,
        Room? room = null,
        IReadOnlyList<SkillPattern>? patterns = null,
        string? commandText = null,
        Organization? org = null,
        string? sigil = null,
        string? messageId = null,
        string? threadId = null,
        DateTime? timestamp = null,
        Conversation? conversation = null,
        bool directMessage = false,
        IPlatformMessage? platformMessage = null,
        string? originalMessage = null,
        MessageInteractionInfo? messageInteractionInfo = null)
    {
        if (platformMessage is not null)
        {
            return FakeMessageContext.Create(platformMessage);
        }

        org ??= TestData.Organization;
        return FakeMessageContext.Create(
            skill ?? string.Empty,
            args,
            originalMessage: originalMessage ?? "some message text",
            messageId: messageId,
            threadId: threadId,
            organization: org,
            sender: from ?? TestData.Member,
            mentions: mentions ?? Array.Empty<Member>(),
            patterns: patterns ?? Array.Empty<SkillPattern>(),
            room: room,
            commandText: commandText,
            sigil: sigil,
            timestamp: timestamp ?? Clock.UtcNow,
            conversation: conversation,
            directMessage: directMessage,
            messageInteractionInfo: messageInteractionInfo);
    }

    public async Task MakeMemberStaffAsync(Member member)
    {
        var role = await Db.Roles.SingleAsync(r => r.Name == Serious.Abbot.Security.Roles.Staff);
        member.MemberRoles.Add(new() { Role = role });
        await Db.SaveChangesAsync();
    }

    public async Task<Member> CreateMemberAsync(
        string? platformUserId = null,
        string? realName = null,
        string? displayName = null,
        string? timeZoneId = null,
        string? email = null,
        Point? location = null,
        WorkingHours? workingHours = null,
        Organization? org = null)
    {
        org ??= TestData.Organization;

        var id = IdGenerator.GetId();
        realName ??= $"Test User {id}";
        var userEventPayload = new UserEventPayload(
            platformUserId ?? IdGenerator.GetSlackUserId(id),
            org.PlatformId,
            realName,
            displayName ?? realName,
            email,
            timeZoneId ?? "America/Vancouver");

        var member = await Users.EnsureAndUpdateMemberAsync(userEventPayload, org);
        if (workingHours is not null || location is not null)
        {
            member.Location = location;
            member.WorkingHours = workingHours;
            await Db.SaveChangesAsync();
        }

        return member;
    }

    public async Task<Playbook> CreatePlaybookAsync(
        string slug = "test",
        string? name = null,
        string? webhookTriggerTokenSeed = null,
        Organization? org = null)
    {
        name ??= slug.Capitalize();
        org ??= TestData.Organization;

        var playbook = new Playbook
        {
            Organization = org,
            Slug = slug,
            Enabled = true,
            Name = name,
            Properties = new PlaybookProperties
            {
                WebhookTokenSeed = webhookTriggerTokenSeed ?? TokenCreator.CreateRandomString(32),
            }
        };
        await Db.AddAsync(playbook);
        await Db.SaveChangesAsync();

        return playbook;
    }

    /// <summary>
    /// Creates a test <see cref="PlaybookVersion"/>.
    /// Default <paramref name="published"/> is <see langword="true"/>,
    /// which sets <see cref="PlaybookVersion.PublishedAt"/> but does not use <see cref="PlaybookPublisher"/>.
    /// </summary>
    /// <param name="playbook"></param>
    /// <param name="definition"></param>
    /// <param name="published"></param>
    /// <returns></returns>
    public async Task<PlaybookVersion> CreatePlaybookVersionAsync(
        PlaybookDefinition definition,
        bool published = true) =>
        await CreatePlaybookVersionAsync(await CreatePlaybookAsync(), definition, published);

    /// <inheritdoc cref="CreatePlaybookVersionAsync(PlaybookDefinition, bool)"></inheritdoc>
    /// <param name="playbook">The <see cref="Playbook"/> that owns the <see cref="PlaybookVersion"/>.</param>
    public async Task<PlaybookVersion> CreatePlaybookVersionAsync(
        Playbook playbook,
        PlaybookDefinition definition,
        bool published = true,
        Member? actor = null)
    {
        actor ??= TestData.Member;

        var playbookVersion = new PlaybookVersion
        {
            PlaybookId = playbook.Id,
            Playbook = playbook,
            Version = 1 + (playbook.Versions.Max(v => (int?)v.Version) ?? 0),
            PublishedAt = published ? Clock.UtcNow : null,
            CreatorId = actor.User.Id,
            Creator = actor.User,
            ModifiedById = actor.User.Id,
            ModifiedBy = actor.User,
            SerializedDefinition = PlaybookFormat.Serialize(definition),
        };

        await Db.PlaybookVersions.AddAsync(playbookVersion);
        await Db.SaveChangesAsync();

        return playbookVersion;
    }

    public StepContext CreateStepContext(
        Playbook playbook,
        Dictionary<string, object?>? inputs = null,
        FakeTemplateEvaluator? templates = null,
        string actionId = "action-1",
        string actionType = "some-action",
        string sequenceId = "seq-1",
        string activityId = "<unknown>")
    {
        var consumeContext = Substitute.For<ConsumeContext>();
        var stepContext = new StepContext(consumeContext)
        {
            ActionReference = new(sequenceId, actionId, 0),
            Step = new ActionStep(actionId, actionType),
            Inputs = inputs ?? new(),
            Playbook = playbook,
            PlaybookRun = new PlaybookRun
            {
                Playbook = playbook,
                Version = 1,
                State = "",
                SerializedDefinition = "",
                Properties = new()
                {
                    ActivityId = activityId,
                },
            },
            TemplateEvaluator = templates,
        };
        return stepContext;
    }

    public async Task<Skill> CreateSkillAsync(
        string name,
        IEnumerable<SignalSubscription>? subscriptions,
        CodeLanguage language = CodeLanguage.CSharp,
        string description = "",
        string codeText = "/* code */",
        string usageText = "The usage text",
        bool restricted = false,
        bool enabled = true,
        string? cacheKey = null,
        Organization? org = null)
    {
        org ??= TestData.Organization;

        var repository = Get<ISkillRepository>();
        var skill = new Skill
        {
            Name = name,
            Description = description,
            Language = language,
            Code = codeText,
            UsageText = usageText,
            Restricted = restricted,
            Organization = org,
            Enabled = enabled
        };

        foreach (var subscription in subscriptions ?? Enumerable.Empty<SignalSubscription>())
        {
            skill.SignalSubscriptions.Add(subscription);
        }

        await repository.CreateAsync(skill, TestData.User);

        if (cacheKey is not null)
        {
            // Overwrite it.
            skill.CacheKey = cacheKey;
        }

        await Db.SaveChangesAsync();
        return skill;
    }

    public async Task<Skill> CreateSkillAsync(
        string name,
        CodeLanguage language = CodeLanguage.CSharp,
        string description = "",
        string codeText = "/* code */",
        string usageText = "The usage text",
        bool restricted = false,
        bool enabled = true,
        IEnumerable<string>? subscriptions = null,
        string? cacheKey = null,
        Organization? org = null)
        => await CreateSkillAsync(
            name,
            subscriptions?.Select(signalName => new SignalSubscription
            {
                Name = signalName
            }),
            language,
            description,
            codeText,
            usageText,
            restricted,
            enabled,

            cacheKey,
            org);

    public async Task<Alias> CreateAliasAsync(string name, string targetSkill, string targetArguments,
        string? description = null, Organization? org = null)
    {
        org ??= TestData.Organization;

        var repository = Get<IAliasRepository>();
        var alias = new Alias
        {
            Name = name,
            TargetSkill = targetSkill,
            TargetArguments = targetArguments,
            Description = description ?? string.Empty,
            Organization = org
        };

        return await repository.CreateAsync(alias, TestData.User);
    }

    public async Task<UserList> CreateListAsync(string name, string description = "", Organization? org = null)
    {
        org ??= TestData.Organization;

        var repository = Get<IListRepository>();
        var list = new UserList
        {
            Name = name,
            Description = description,
            Organization = org
        };

        return await repository.CreateAsync(list, TestData.User);
    }

    public async Task<Member> CreateMemberInAgentRoleAsync(
        string? platformUserId = null,
        string? displayName = null,
        string? email = null,
        bool isDefaultResponder = false,
        string? timeZoneId = null,
        WorkingHours? workingHours = null,
        Organization? org = null)
    {
        return await CreateMemberInRoleAsync(
            Serious.Abbot.Security.Roles.Agent,
            platformUserId: platformUserId,
            displayName: displayName,
            email: email,
            isDefaultResponder: isDefaultResponder,
            timeZoneId: timeZoneId,
            workingHours: workingHours,
            org: org);
    }

    public async Task<Member> CreateAdminMemberAsync(Organization? org = null)
    {
        return await CreateMemberInRoleAsync(Serious.Abbot.Security.Roles.Administrator, org: org);
    }

    public async Task<Member> CreateStaffMemberAsync(Organization? org = null)
    {
        return await CreateMemberInRoleAsync(Serious.Abbot.Security.Roles.Staff, org: org);
    }

    public async Task<Member> CreateMemberInRoleAsync(
        string? roleName,
        string? platformUserId = null,
        string? displayName = null,
        string? email = null,
        bool isDefaultResponder = false,
        string? timeZoneId = null,
        WorkingHours? workingHours = null,
        Organization? org = null)
    {
        org ??= TestData.Organization;
        var member = await CreateMemberAsync(
            platformUserId: platformUserId,
            displayName: displayName,
            email: email,
            timeZoneId: timeZoneId,
            workingHours: workingHours,
            org: org);
        member.IsDefaultFirstResponder = isDefaultResponder;
        member.User.NameIdentifier = $"oauth2|slack|{member.User.PlatformUserId}";
        await AddUserToRoleAsync(member, roleName);
        return member;
    }

    /// <summary>
    /// Like <see cref="IRoleManager.AddUserToRoleAsync(Member, string, Member, string?)"/>,
    /// but without auditing.
    /// </summary>
    public async Task AddUserToRoleAsync(Member member, string? roleName)
    {
        if (roleName is null)
        {
            return;
        }

        var role = await Roles.GetRoleAsync(roleName);
        member.MemberRoles.Add(new MemberRole
        {
            Role = role
        });

        await Db.SaveChangesAsync();
    }

    /// <summary>
    /// Like <see cref="IRoomRepository.AssignMemberAsync(Room, Member, RoomRole, Member)"/>,
    /// but without auditing.
    /// </summary>
    public async Task AssignMemberAsync(Room room, Member member, RoomRole roomRole)
    {
        room.Assignments.Add(new RoomAssignment { Member = member, Role = roomRole });
        await Db.SaveChangesAsync();
    }

    /// <summary>
    /// Like <see cref="IRoomRepository.SetRoomAssignmentsAsync(Room, IEnumerable{string}, RoomRole, Member)"/>,
    /// but without auditing.
    /// </summary>
    public async Task SetRoomAssignmentsAsync(Room room, IEnumerable<string> selectedIds, RoomRole roomRole)
    {
        var members = await Db.Members
            .Where(m => selectedIds.Contains(m.User.PlatformUserId))
            .ToListAsync();
        room.Assignments.AddRange(
            members.Select(m => new RoomAssignment { Member = m, Role = roomRole }));
        await Db.SaveChangesAsync();
    }

    /// <summary>
    /// Like <see cref="IOrganizationRepository.AssignDefaultFirstResponderAsync(Organization, Member, Member)"/>,
    /// but without auditing.
    /// </summary>
    public async Task<Member> AssignDefaultFirstResponderAsync(Member subject)
    {
        Assert.True(!subject.User.IsBot);
        Assert.True(subject.IsAgent());
        subject.IsDefaultFirstResponder = true;
        await Db.SaveChangesAsync();
        return subject;
    }

    /// <summary>
    /// Like <see cref="IOrganizationRepository.AssignDefaultFirstResponderAsync(Organization, Member, Member)"/>,
    /// but without auditing.
    /// </summary>
    public async Task<Member> AssignDefaultEscalationResponderAsync(Member subject)
    {
        Assert.True(!subject.User.IsBot);
        Assert.True(subject.IsAgent());
        subject.IsDefaultEscalationResponder = true;
        await Db.SaveChangesAsync();
        return subject;
    }

    public async Task<Memory> CreateMemoryAsync(string name, string content, User? creator = null,
        Organization? org = null)
    {
        var memory = new Memory
        {
            Name = name,
            Content = content,
            Organization = org ?? TestData.Organization
        };

        return await Memories.CreateAsync(memory, creator ?? TestData.User);
    }

    public async Task CreateMetricObservationAsync(
        DateTime timestamp,
        Conversation conversation,
        string metric,
        double value)
    {
        await Db.MetricObservations.AddAsync(new(timestamp,
            metric,
            conversation.Id,
            conversation.Room.Id,
            conversation.Organization.Id,
            value));

        await Db.SaveChangesAsync();
    }

    public Task<Integration> EnableIntegrationAsync(
        IntegrationType integrationType,
        string? externalId = null,
        Organization? organization = null) =>
        CreateIntegrationAsync(integrationType, enabled: true, externalId, organization);

    public async Task<Integration> CreateIntegrationAsync(
        IntegrationType integrationType,
        bool enabled = false,
        string? externalId = null,
        Organization? organization = null)
    {
        var integration = new Integration
        {
            Organization = organization ?? TestData.Organization,
            Type = integrationType,
            ExternalId = externalId,
            Enabled = enabled,
            Settings = "{}",
        };
        await Db.Integrations.AddAsync(integration);
        await Db.SaveChangesAsync();
        return integration;
    }

    public Task<Integration> EnableIntegrationAsync<TSettings>(
        TSettings settings,
        string? externalId = null,
        Organization? organization = null)
        where TSettings : class, IIntegrationSettings =>
        CreateIntegrationAsync(settings, enabled: true, externalId, organization);

    public async Task<Integration> CreateIntegrationAsync<TSettings>(
        TSettings settings,
        bool enabled = false,
        string? externalId = null,
        Organization? organization = null)
        where TSettings : class, IIntegrationSettings
    {
        var integration = await CreateIntegrationAsync(
            TSettings.IntegrationType, enabled, externalId, organization);
        await Integrations.SaveSettingsAsync(integration, settings);
        return integration;
    }

    public async Task<Customer> CreateCustomerAsync(string name = "Test Customer", IEnumerable<string>? segments = null)
    {
        var customer = new Customer
        {
            Name = name,
        };

        if (segments is not null)
        {
            foreach (var segment in segments)
            {
                var tag = await Db.CustomerTags.FirstOrDefaultAsync(t => t.Name == segment)
                    ?? new CustomerTag { Name = segment };
                customer.TagAssignments.Add(new()
                {
                    Customer = customer,
                    CustomerId = 0,
                    Tag = tag,
                    TagId = 0,
                });
            }
        }

        await Db.AddAsync(customer);
        await Db.SaveChangesAsync();
        return customer;
    }

    public async Task<Room> CreateRoomAsync(
        RoomFlags flags,
        string? platformRoomId = null,
        string? name = null,
        RoomType roomType = RoomType.PublicChannel,
        IEnumerable<Member>? firstResponders = null,
        IEnumerable<Member>? escalationResponders = null,
        Hub? hub = null,
        Customer? customer = null,
        Organization? org = null) =>
        await CreateRoomAsync(
            platformRoomId,
            name,
            managedConversationsEnabled: flags.HasFlag(RoomFlags.ManagedConversationsEnabled),
            persistent: !flags.HasFlag(RoomFlags.NotPersistent),
            archived: flags.HasFlag(RoomFlags.Archived),
            deleted: flags.HasFlag(RoomFlags.Deleted),
            botIsMember: !flags.HasFlag(RoomFlags.BotIsNotMember),
            shared: flags.HasFlag(RoomFlags.Shared),
            isCommunity: flags.HasFlag(RoomFlags.IsCommunity),
            roomType,
            firstResponders,
            escalationResponders,
            hub,
            customer,
            org);

    public async Task<Room> CreateRoomAsync(
        string? platformRoomId = null,
        string? name = null,
        bool managedConversationsEnabled = false,
        bool persistent = true,
        bool archived = false,
        bool deleted = false,
        bool? botIsMember = true,
        bool shared = true,
        bool isCommunity = false,
        RoomType roomType = RoomType.PublicChannel,
        IEnumerable<Member>? firstResponders = null,
        IEnumerable<Member>? escalationResponders = null,
        Hub? hub = null,
        Customer? customer = null,
        Organization? org = null)
    {
        org ??= TestData.Organization;
        var id = IdGenerator.GetId();
        var roomId = platformRoomId ?? IdGenerator.GetSlackChannelId(id);

        if (managedConversationsEnabled && !persistent)
        {
            throw new ArgumentException(
                $"{nameof(persistent)} must be true if {nameof(managedConversationsEnabled)} is true");
        }

        var room = new Room
        {
            PlatformRoomId = roomId,
            Name = name ?? $"Test Room {id}",
            Organization = org,
            Customer = customer,
            Hub = hub,
            Persistent = persistent,
            ManagedConversationsEnabled = managedConversationsEnabled,
            Assignments = new List<RoomAssignment>(),
            Deleted = deleted,
            Archived = archived,
            BotIsMember = botIsMember,
            RoomType = roomType,
            Shared = shared
        };

        if (isCommunity)
        {
            room.Settings = new()
            {
                IsCommunityRoom = isCommunity,
            };
        }

        if (firstResponders is not null)
        {
            room.Assignments.AddRange(firstResponders
                .Select(fr => new RoomAssignment
                {
                    Member = fr,
                    Role = RoomRole.FirstResponder,
                }));
        }

        if (escalationResponders is not null)
        {
            room.Assignments.AddRange(escalationResponders
                .Select(fr => new RoomAssignment
                {
                    Member = fr,
                    Role = RoomRole.EscalationResponder,
                }));
        }

        return await Rooms.CreateAsync(room);
    }

    public async Task<Hub> CreateHubAsync(
        string? name = null,
        string? roomPlatformId = null,
        Member? actor = null,
        Action<Hub>? config = null)
    {
        name ??= "test-hub";
        var room = await CreateRoomAsync(roomPlatformId ?? "Chub", name);
        var hub = new Hub()
        {
            Name = name,
            Room = room,
            RoomId = room.Id,
            OrganizationId = room.OrganizationId,
            Created = Clock.UtcNow,
        };

        if (config is not null)
        {
            config(hub);
        }

        await Db.Hubs.AddAsync(hub);
        await Db.SaveChangesAsync();

        return hub;
    }

    public async Task<Announcement> CreateAnnouncementAsync(
        Room sourceRoom,
        string sourceMessageId,
        DateTime? scheduledDateUtc = null,
        params Room[] targetRooms)
    {
        var announcement = new Announcement
        {
            ScheduledDateUtc = scheduledDateUtc,
            Creator = TestData.User,
            ModifiedBy = TestData.User,
            SourceRoom = sourceRoom,
            SourceMessageId = sourceMessageId,
            Organization = TestData.Organization,
            Messages = targetRooms.Select(r => new AnnouncementMessage
            {
                Room = r,
            })
                .ToList(),
            CustomerSegments = new List<AnnouncementCustomerSegment>(),
        };

        await Db.Announcements.AddAsync(announcement);
        await Db.SaveChangesAsync();
        return announcement;
    }

    public async Task<Conversation> CreateConversationAsync(
        Room room,
        string? title = null,
        DateTimeOffset? timestamp = null,
        Member? startedBy = null,
        string? firstMessageId = null,
        bool createFirstMessageEvent = false,
        DateTime? importedOnUtc = null,
        ConversationProperties? properties = null,
        ConversationState initialState = ConversationState.New)
    {
        startedBy ??= TestData.Member;

        var utcTimestamp = timestamp?.UtcDateTime ?? Clock.UtcNow;

        var id = IdGenerator.GetId();
        firstMessageId ??= IdGenerator.GetSlackMessageId(id);

        var conversation = new Conversation
        {
            Room = room,
            State = initialState,
            Organization = room.Organization,
            FirstMessageId = firstMessageId,
            Created = utcTimestamp,
            LastStateChangeOn = utcTimestamp,
            LastMessagePostedOn = utcTimestamp,
            StartedBy = startedBy,
            ImportedOn = importedOnUtc,
            Title = title ?? $"Test Conversation {id}",
            Members = new List<ConversationMember>
            {
                new()
                {
                    Member = startedBy,
                    JoinedConversationAt = utcTimestamp,
                    LastPostedAt = utcTimestamp,
                }
            },
            Properties = properties ?? new(),
            ThreadIds = new List<string> { firstMessageId },
        };
        if (createFirstMessageEvent)
        {
            conversation.Events.Add(new MessagePostedEvent
            {
                Conversation = conversation,
                Member = startedBy,
                Created = utcTimestamp,
                MessageId = firstMessageId,
                MessageUrl = new Uri($"https://example.com/messages/{firstMessageId}"),
                ThreadId = conversation.FirstMessageId,
                Metadata = new MessagePostedMetadata
                {
                    Categories = Array.Empty<Category>(),
                    Text = new(conversation.Title),
                    SensitiveValues = Array.Empty<SensitiveValue>(),
                }.ToJson(),
            });
        }
        await Db.Conversations.AddAsync(conversation);
        await Db.SaveChangesAsync();
        return conversation;
    }

    public async Task<ConversationLink> CreateConversationLinkAsync(
        Conversation conversation,
        ConversationLinkType linkType,
        string externalId,
        JsonSettings? settings = null,
        Member? actor = null)
    {
        var link = new ConversationLink
        {
            Conversation = conversation,
            Organization = conversation.Organization,
            LinkType = linkType,
            ExternalId = externalId,
            CreatedBy = actor ?? TestData.Member,
            Created = Clock.UtcNow,
            Settings = settings?.ToJson(),
        };
        await Db.ConversationLinks.AddAsync(link);
        await Db.SaveChangesAsync();
        return link;
    }

    public FakeControllerContext CreateControllerContext(User? user = null, Organization? org = null)
    {
        user ??= TestData.User;
        org ??= TestData.Organization;
        IEnumerable<Claim> claims = new[]
        {
            new Claim($"{AbbotSchema.SchemaUri}platform_user_id", user.PlatformUserId),
            new Claim($"{AbbotSchema.SchemaUri}platform_id", org.PlatformId)
        };

        if (user.NameIdentifier is not null)
        {
            claims = claims.Append(new Claim($"{AbbotSchema.SchemaUri}platform_user_id", user.PlatformUserId));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        return new FakeControllerContext(principal);
    }

    public async Task<IReadOnlyList<AuditEventBase>> GetAllActivityAsync(Organization? organization = null) =>
        (await AuditLog.GetRecentActivityAsync(
            organization ?? TestData.Organization,
            activityTypeFilter: ActivityTypeFilter.All))
            .Reverse() // Easier to read snapshots in chronological order
            .ToList();
}

public class TestEnvironmentWithData<TData> : TestEnvironmentWithData
    where TData : CommonTestData
{
    public new TData TestData => (TData)base.TestData;

    [Obsolete("Only for DI purposes")]
    public TestEnvironmentWithData(IServiceProvider services, IServiceScopeFactory scopeFactory, CommonTestData testData) : base(services, scopeFactory, testData)
    {
        if (testData is not TData)
        {
            throw new InvalidOperationException(
                $"You need to replace the CommonTestData service with the custom {typeof(TData).FullName} type. TestEnvironment.Create<T> will do this for you!");
        }
    }
}
