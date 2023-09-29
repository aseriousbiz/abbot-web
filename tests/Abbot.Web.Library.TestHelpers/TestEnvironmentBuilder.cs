using System.Text.Encodings.Web;
using Abbot.Common.TestHelpers.Fakes;
using Hangfire;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Segment;
using Serious;
using Serious.Abbot.AI;
using Serious.Abbot.AppStartup;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Clients;
using Serious.Abbot.Compilation;
using Serious.Abbot.Configuration;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing;
using Serious.Abbot.Forms;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.AppStartup;
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
using Serious.Abbot.Onboarding;
using Serious.Abbot.PageServices;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Rooms;
using Serious.Abbot.Routing;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Serious.Abbot.Services.DefaultResponder;
using Serious.Abbot.Signals;
using Serious.Abbot.Storage.FileShare;
using Serious.Abbot.Telemetry;
using Serious.Abbot.Validation;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BotFramework;
using Serious.TestHelpers;
using Xunit.Abstractions;

namespace Abbot.Common.TestHelpers;

public static class TestEnvironmentBuilder
{
    /// <summary>
    /// Creates a <see cref="TestEnvironment"/> to execute tests in.
    /// The environment will have no test data created.
    /// </summary>
    /// <returns>The constructed <see cref="TestEnvironment"/></returns>
    public static TestEnvironmentBuilder<TestEnvironment> CreateWithoutData() =>
        TestEnvironmentBuilder<TestEnvironment>.Create();

    /// <summary>
    /// Creates a <see cref="TestEnvironmentWithData"/> to execute tests in.
    /// The environment will contain test data.
    /// </summary>
    /// <returns>The constructed <see cref="TestEnvironmentWithData"/></returns>
    public static TestEnvironmentBuilder<TestEnvironmentWithData> Create() =>
        TestEnvironmentBuilder<TestEnvironmentWithData>.Create();

    /// <summary>
    /// Creates a <see cref="TestEnvironmentWithData{TData}"/> to execute tests in.
    /// The environment will contain test data in the form of an instance of <typeparamref name="TData"/>.
    /// </summary>
    /// <typeparam name="TData">A type deriving from <see cref="CommonTestData"/> which seeds additional test data for this test.</typeparam>
    /// <returns>The constructed <see cref="TestEnvironmentWithData{TData}"/>.</returns>
    public static TestEnvironmentBuilder<TestEnvironmentWithData<TData>> Create<TData>()
        where TData : CommonTestData =>
        TestEnvironmentBuilder<TestEnvironmentWithData<TData>>.Create().ReplaceService<CommonTestData, TData>();
}

public class TestEnvironmentBuilder<TEnvironment> where TEnvironment : class
{
    static readonly HashSet<Type> BlockedSubstitutes = new()
    {
        typeof(IUserRepository),
        typeof(IRoomRepository),
        typeof(IOrganizationRepository),
        typeof(IConversationRepository),
    };

    readonly List<Action<IBusRegistrationConfigurator>> _busConfigs = new();

    public IServiceCollection Services { get; }

    public TestEnvironmentBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// Builds <typeparamref name="TEnvironment"/> from configured <see cref="Services"/>.
    /// </summary>
    /// <param name="snapshot">
    /// Configures the environment for snapshot stability.
    /// Requires <typeparamref name="TEnvironment"/> inherit from <see cref="TestEnvironment"/>.
    /// See <see cref="TestEnvironment.ConfigureForSnapshot"/> for details.
    /// </param>
    /// <returns>The <typeparamref name="TEnvironment"/>.</returns>
    public TEnvironment Build(bool snapshot = false)
    {
        // Configure Mass Transit right now, before building.
        // This way, all the collected configuration is applied.
        Services.AddMassTransitTestHarness(config => {
            // We don't register MassTransit consumers automatically!
            // This is intentional, so that tests can opt-in to which consumers they want in their test flow.
            foreach (var configurator in _busConfigs)
            {
                configurator(config);
            }

            // Register our test helper consume filter.
            // This allows us to await consumption of messages from the main test body
            config.AddConsumeObserver<ConsumerTestObserver>(s => s.GetRequiredService<ConsumerTestObserver>());

            // We don't AddAbbotEventingConfig here because it fills our logs with Diagnostic*Filter messages and we don't want that right now.
            config.UsingInMemory((context, cfg) => {
                cfg.UsePublishMessageScheduler();
                cfg.ConfigureAbbotSerialization();
                // We don't ConfigureAbbotTopology here because it fills our logs with Diagnostic*Filter messages and we don't want that right now.
                cfg.ConfigureEndpoints(context);
                cfg.UseAbbotFilters(context);
            });
        });

        if (snapshot)
        {
            // We have to override the time-travel clock to be at TestOClock before even constructing the environment.
            // Because some services may use time in their constructors.
            Services.AddSingleton<IClock>(_ => {
                var clock = new TimeTravelClock();
                clock.TravelTo(TestEnvironment.TestOClock);
                return clock;
            });
        }

        // Validate scopes, it's way easier to debug issues if we do that.
        // It has a performance impact, which is why it's off by default in apps, but it's negligible for tests.
        var provider = Services.BuildServiceProvider(true);
        var env = provider.Activate<TEnvironment>();
        if (snapshot)
        {
            var testEnv = Assert.IsAssignableFrom<TestEnvironment>(env);
            testEnv.ConfigureForSnapshot();
        }
        return env;
    }

    public TestEnvironmentBuilder<TEnvironment> Configure(Action<TestEnvironmentBuilder<TEnvironment>> configure)
    {
        configure(this);
        return this;
    }

    public TestEnvironmentBuilder<TEnvironment> Configure<TOptions>(Action<TOptions> configure) where TOptions : class
    {
        Services.Configure(configure);
        return this;
    }

    public TestEnvironmentBuilder<TEnvironment> Substitute<T>() where T : class => Substitute<T>(out _);

    public TestEnvironmentBuilder<TEnvironment> Substitute<T>(out T substitute) where T : class
    {
        if (BlockedSubstitutes.Contains(typeof(T)))
        {
            throw new InvalidOperationException($"{typeof(T)} is blocked from being substituted.");
        }

        substitute = NSubstitute.Substitute.For<T>();
        return ReplaceService(substitute);
    }

    public TestEnvironmentBuilder<TEnvironment> ReplaceService<TInterface, TInstance>(ServiceLifetime? lifetime = null)
        where TInterface : class
        where TInstance : class, TInterface
    {
        // Ok, this is sneaky, but it should work.
        // If a lifetime isn't provided, but there is already a service registered for TInterface,
        // we just find it's descriptor and use the lifetime from that service.
        var lifetimeToUse = lifetime is { } l
            ? l
            : InferLifetime<TInterface>(Services);

        Services.Add(ServiceDescriptor.Describe(
            typeof(TInterface),
            typeof(TInstance),
            lifetimeToUse));

        return this;
    }

    public TestEnvironmentBuilder<TEnvironment> ReplaceService<T>(T instance) where T : class
    {
        // This only works for singletons.
        Services.AddSingleton(instance);
        return this;
    }

    public TestEnvironmentBuilder<TEnvironment> ReplaceService<T>(Func<IServiceProvider, T> implementationFactory,
        ServiceLifetime? lifetime = null) where T : class
    {
        // Ok, this is sneaky, but it should work.
        // If a lifetime isn't provided, but there is already a service registered for TInterface,
        // we just find it's descriptor and use the lifetime from that service.
        var lifetimeToUse = lifetime is { } l
            ? l
            : InferLifetime<T>(Services);

        Services.Add(ServiceDescriptor.Describe(
            typeof(T),
            implementationFactory,
            lifetimeToUse));

        Services.AddSingleton(implementationFactory);
        return this;
    }

    public static TestEnvironmentBuilder<TEnvironment> Create(Action<IServiceCollection>? configureServices = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IdGenerator>();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<TEnvironment>();
        if (typeof(TestEnvironment).IsAssignableFrom(typeof(TEnvironment)))
        {
            // We know this cast will work, so we do some silliness by casting through object to convince the compiler.
            services.AddSingleton(sp =>
                (TestEnvironment)(object)sp.GetRequiredService<TEnvironment>());
        }

        ConfigureServices(services, config);
        configureServices?.Invoke(services);
        return new TestEnvironmentBuilder<TEnvironment>(services);
    }

    public TestEnvironmentBuilder<TEnvironment> ConfigureBus(Action<IBusRegistrationConfigurator> cfg)
    {
        _busConfigs.Add(cfg);
        return this;
    }

    public TestEnvironmentBuilder<TEnvironment> AddAbbotSagaConfig()
    {
        return ConfigureBus(cfg => cfg.AddAbbotSagaConfig());
    }

    public TestEnvironmentBuilder<TEnvironment> AddBusConsumer<T>()
        where T : class, IConsumer
    {
        return ConfigureBus(cfg => cfg.AddConsumer<T>());
    }

    public TestEnvironmentBuilder<TEnvironment> ConfigureServices(Action<IServiceCollection> configureServices)
    {
        configureServices(Services);
        return this;
    }

    public TestEnvironmentBuilder<TEnvironment> UseTestOutputLogging(ITestOutputHelper testOutput)
    {
        Services.AddSingleton(testOutput);
        return this;
    }

    static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FakeAbbotContextOptions>(options => {
            // Set a database name for this test.
            // This means all tests get their own in memory database, BUT all instances of FakeAbbotContext share the same database.
            options.DatabaseName = Path.GetRandomFileName();
        });

        var dataProtectionProvider = new FakeDataProtectionProvider();
        SecretString.Configure(dataProtectionProvider);
        services.AddSingleton<IDataProtectionProvider>(dataProtectionProvider);

        // Need to replace both ILoggerFactory and ILoggerProvider because Mass Transit's Test Harnesses try to _register_ loggers, which requires ILoggerProvider.
        services.AddSingleton<FakeLoggerProvider>();
        services.AddSingleton<ILoggerFactory>(s => s.GetRequiredService<FakeLoggerProvider>());
        services.AddSingleton<ILoggerProvider>(s => s.GetRequiredService<FakeLoggerProvider>());

        services.RegisterAllHandlers();
        services.AddTimeTravelClock();
        services.AddSingleton<IStopwatchFactory, FakeStopwatchFactory>();
        services.AddScoped<IDbContextFactory<AbbotContext>>(sp => new FakeDbContextFactory<AbbotContext>(sp));
        services.AddScoped<AbbotContext, FakeAbbotContext>();

        services.AddRepositories();
        services.AddPlaybookServices();
        services.AddOnboardingServices();

        // Override a few repositories with fakes because it's easier to test with them.
        services.AddScoped<IInsightsRepository, FakeInsightsRepository>();

        services.AddSingleton<IBotFrameworkAdapter, FakeBotFrameworkAdapter>();
        services.AddScoped<BlockKitToHtmlFormatter>();
        services.AddScoped<ConversationMessageToHtmlFormatter>();
        services.AddScoped<IConversationRepository, FakeConversationRepository>();
        services.AddScoped<IMemoryRepository, FakeMemoryRepository>();
        services.AddScoped<CoverageHoursResponseTimeCalculator>();
        services.AddSingleton<IHostEnvironment, FakeHostEnvironment>();
        services.AddSingleton<IHttpClientFactory, FakeHttpClientFactory>();
        services.AddSingleton<HttpMessageHandler, FakeHttpMessageHandler>();
        services.AddSingleton<ProblemDetailsFactory, FakeProblemDetailsFactory>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IApiTokenFactory, ApiTokenFactory>();
        services.AddSingleton<IAssemblyCache, FakeAssemblyCache>();
        services.AddSingleton<IAssemblyCacheClient, FakeAssemblyCacheClient>();
        services.AddSingleton<IAuthenticationService, FakeAuthenticationService>();
        services.AddSingleton<IAzureKeyVaultClient, FakeAzureKeyVaultClient>();
        services.AddSingleton<IBackgroundJobClient, FakeBackgroundJobClient>();
        services.AddSingleton<ICachingCompilerService, FakeCachingCompilerService>();
        services.AddSingleton<ISensitiveLogDataProtector, FakeSensitiveLogDataProtector>();
        services.AddSingleton<IEmojiLookup, EmojiLookup>();
        services.AddSingleton(NSubstitute.Substitute.For<ICustomEmojiLookup>());
        services.AddSingleton<IMemoryCache, FakeMemoryCache>();
        services.AddSingleton<ISlackApiClient, FakeSimpleSlackApiClient>();
        services.AddScoped<IBotFrameworkHttpAdapter, FakeBotFrameworkAdapter>();
        services.AddSingleton(s => s.GetService<ISlackApiClient>()!.Conversations);
        services.AddSingleton(s => s.GetService<ISlackApiClient>()!.Reactions);
        services.AddSingleton(s => s.GetService<ISlackApiClient>()!.Files);
        services.AddScoped<IBackgroundSlackClient, FakeBackgroundSlackClient>();
        services.AddScoped<TicketModalService>();
        services.AddScoped<TicketNotificationService>();
        services.AddSingleton<ITicketIntegrationService, TicketIntegrationService>();
        services.AddScoped<DismissHandler>();
        services.AddScoped<IMessageDispatcher, SlackMessageDispatcher>();
        services.AddSingleton<Reactor>();
        services.AddAnnouncementDispatcherAndTargets();

        services.AddConversationTracking(includeListeners: false);
        services.AddScoped<IConversationPublisher, FakeConversationPublisher>();
        services.AddScoped<IConversationTracker, FakeConversationTracker>();
        services.AddSingleton<IConversationThreadResolver, FakeConversationThreadResolver>();
        services.AddSingleton<IConversationListener, FakeConversationListener>();
        services.AddSingleton<IMissingConversationsReporter, MissingConversationsReporter>();

        services.AddApiServices();
        services.AddTransient<IOrganizationApiSyncer, OrganizationApiSyncer>();
        services.AddTransient<IRoomJoiner, RoomJoiner>();
        services.AddScoped<IAuditLog, FakeAuditLog>();
        services.AddSingleton<ISignalHandler, FakeSignalHandler>();
        services.AddSingleton<ISystemSignaler, FakeSystemSignaler>();
        services.AddFeatureFlagServices(configuration.GetSection("FeatureFlags"));
        services.AddSingleton<IFeatureManager, FakeFeatureManager>();
        services.AddSingleton<IFeatureManagerSnapshot>(sp => (IFeatureManagerSnapshot)sp.GetRequiredService<IFeatureManager>());
        services.AddScoped<ISlackResolver, SlackResolver>();
        services.AddSingleton<SlackEventDeduplicator>();
        services.AddSingleton<IGeocodeService, FakeGeocodingService>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IProactiveMessenger, ProactiveMessenger>();
        services.AddScoped<IRoleManager, RoleManager>();
        services.AddScoped<ISkillManifest, SkillManifest>();
        services.AddScoped<ISkillSecretRepository, SkillSecretRepository>();
        services.AddScoped<ISkillNotFoundHandler, SkillNotFoundHandler>();
        services.AddSingleton<IAnalyticsClient, FakeAnalyticsClient>();
        services.AddSingleton<IDefaultResponderService, FakeDefaultResponderService>();
        services.AddSingleton<IPayloadHandlerRegistry, PayloadHandlerRegistry>();
        services.AddSingleton<IResponder, FakeResponder>();
        services.AddSingleton<IScriptVerifier, ScriptVerifier>();
        services.AddScoped<ISettingsManager, SettingsManager>();
        services.AddScoped<ISkillAuditLog, SkillAuditLog>();
        services.AddSingleton<ISkillCompiler, FakeSkillCompiler>();
        services.AddScoped<ISkillNameValidator, SkillNameValidator>();
        services.AddScoped<ISkillEditorService, SkillEditorService>();
        services.AddScoped<ISkillPatternMatcher, SkillPatternMatcher>();
        services.AddScoped<ISkillRouter, SkillRouter>();
        services.AddScoped<IRunnerEndpointManager, RunnerEndpointManager>();
        services.AddScoped<IScheduledSkillClient, ScheduledSkillClient>();
        services.AddSingleton<ISkillRunnerClient, FakeSkillRunnerClient>();
        services.AddSingleton<ISkillRunnerRetryPolicy, FakeSkillRunnerRetryPolicy>();
        services.AddSingleton<ITempDataProvider, FakeTempDataProvider>();
        services.AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>();
        services.AddSingleton<IBuiltinSkillRegistry, FakeBuiltinSkillRegistry>();
        services.AddSingleton<IRecurringJobManager, FakeRecurringJobManager>();
        services.AddSingleton<IUrlGenerator, FakeUrlGenerator>();
        services.AddSingleton<IUrlHelperFactory, FakeUrlHelperFactory>();
        services.AddSingleton<IActionResultExecutor<ContentResult>, FakeActionResultExecutor<ContentResult>>();
        services.AddTransient<ITurnContextTranslator, UnitTestTurnContextTranslator>();
        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddTransient<IMessageRenderer, FakeMessageRenderer>();
        services.AddTransient<OpenTicketMessageBuilder>();
        services.AddSingleton<IRouter, FakeRouter>();
        services.AddScoped<IBotInstaller, BotInstaller>();
        services.AddSingleton<CommonTestData>();
        services.AddLogging();
        services.AddSingleton(UrlEncoder.Default);
        services.AddSingleton<ISystemClock, AuthSystemClockAdapter>();
        services.AddOptions();
        services.Configure<SkillOptions>(options => {
            options.DotNetEndpoint = "http://localhost:7071/api/skillrunner";
            options.JavaScriptEndpoint = "http://localhost:7072/api/skillrunner";
            options.PythonEndpoint = "http://localhost:7073/api/skillrunner";
            options.InkEndpoint = "http://localhost:7071/api/skillrunner";
            options.DataApiKey = "IRWb9gsquqVuwh5TQRLA5h/T18zMIPt2vmnYi6+u/XK5q0kJwTVLwiuBaqsTLYUknXh1F2p9vXZ2IVUh3jpw";
        });

        services.AddScoped<IIntegrationRepository, FakeIntegrationRepository>();
        services.AddZendeskIntegrationServices(configuration.GetSection("Zendesk"));
        services.AddSingleton<IZendeskClientFactory, FakeZendeskClientFactory>();
        services.AddScoped<ISlackThreadExporter, SlackThreadExporter>();
        services.AddSingleton<ISlackToZendeskCommentImporter, FakeSlackToZendeskCommentImporter>();
        services.AddGitHubIntegrationServices(configuration.GetSection("GitHub"));
        services.AddSingleton<IGitHubClientFactory, FakeGitHubClientFactory>();
        services.AddHubSpotIntegrationServices(configuration.GetSection("HubSpot"));
        services.AddSingleton<IHubSpotClientFactory, FakeHubSpotClientFactory>();
        services.AddMergeDevIntegrationServices(configuration.GetSection("MergeDev"));
        services.AddSingleton<IMergeDevClientFactory, FakeMergeDevClientFactory>();

        services.AddScoped<ISlackIntegration, SlackIntegration>();
        services.AddSingleton<ICorrelationService, CorrelationService>();

        services.AddSingleton<IModelMetadataProvider, EmptyModelMetadataProvider>();
        services.AddFormsServices();

        // We don't register MassTransit consumers automatically!
        // This is intentional, so that tests can opt-in to which consumers they want in their test flow.
        // But we do register test services
        services.AddSingleton<ConsumerTestObserver>();

        services.AddSingleton<IFlashPublisher, FakeFlashPublisher>();

        services.AddAbbotAIServices(configuration);
        services.AddSingleton<IOpenAIClient, FakeOpenAiClient>();
        services.AddScoped<ITextAnalyticsClient, FakeTextAnalyticsClient>();
    }

    static ServiceLifetime InferLifetime<TInterface>(IServiceCollection services)
    {
        // Use the lifetime of the most recently registered descriptor.
        var match = services.LastOrDefault(s => s.ServiceType == typeof(TInterface))
                    ?? throw new InvalidOperationException(
                        $"Cannot infer lifetime, no service of type {typeof(TInterface)} was registered.");

        return match.Lifetime;
    }
}
