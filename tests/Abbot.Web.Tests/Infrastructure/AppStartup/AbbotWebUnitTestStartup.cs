#pragma warning disable CS0618 // Type or member is obsolete in newer AspNetCore
using Abbot.Common.TestHelpers.Fakes;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using NSubstitute;
using Segment;
using Serious;
using Serious.Abbot.Clients;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Messages;
using Serious.Abbot.Routing;
using Serious.Abbot.Services.DefaultResponder;
using Serious.Abbot.Storage.FileShare;
using Serious.Abbot.Web;
using Serious.Slack;
using Serious.Slack.AspNetCore;
using Serious.TestHelpers;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
#pragma warning restore CS0618

public class AbbotWebUnitTestStartup
{
    [Fact]
    public void ValidateDependencies()
    {
        // if any services are missing, this will throw
        using var _ = new ScopedProvider(sc => CreateServiceProvider(sc, null));
    }

    [Fact]
    public void ValidateDependenciesWithAzureSysContext()
    {
        // if any services are missing, this will throw
        using var _ = new ScopedProvider(CreateServiceProviderWithAzureSysContext);

        ServiceProvider CreateServiceProviderWithAzureSysContext(IServiceCollection serviceCollection)
        {
            var sp = CreateServiceProvider(serviceCollection, new Dictionary<string, string?>()
            {
                { "ConnectionStrings:AzureSysContext", "Database=azure_sys" }
            });
            return sp;
        }
    }

    public static ServiceProvider CreateAbbotTestAdapterServiceProvider()
    {
        return CreateServiceProvider(new ServiceCollection(), null);
    }

    static ServiceProvider CreateServiceProvider(IServiceCollection serviceCollection, IEnumerable<KeyValuePair<string, string?>>? configValues)
    {
        var hostEnvironment = new FakeHostEnvironment();
        var configuration = CreateConfiguration(configValues);
        serviceCollection.AddSingleton(configuration);
        var slackApiClient = new FakeSimpleSlackApiClient(IClock.System);
        slackApiClient.Conversations.AddConversationInfoResponse("the-api-token",
            new ConversationInfo
            {
                Id = "C01A3DGTSP9",
                Name = "the-room"
            });

        var dataProtectionProvider = new FakeDataProtectionProvider();
        var backgroundSlackClient = new FakeBackgroundSlackClient();
        var startup = new Startup(configuration, hostEnvironment);
        startup.ConfigureServices(serviceCollection);
        var httpClientFactory = new FakeHttpClientFactory();
        serviceCollection.ReplaceIt<IHttpClientFactory>(httpClientFactory, ServiceLifetime.Singleton);
        httpClientFactory.AddHttpClient("IGeocodeService", new HttpClient());
        httpClientFactory.AddHttpClient("IDefaultResponderService", new HttpClient());

        serviceCollection.ReplaceIt<IHostEnvironment>(new FakeHostEnvironment
        {
            EnvironmentName = "UnitTest"
        });
#pragma warning disable CS0618 // Type or member is obsolete
        // Dependency of Microsoft.AspNetCore.Hosting.DefaultApplicationInsightsServiceConfigureOptions
        serviceCollection.ReplaceIt<IHostingEnvironment>(sp =>
            (FakeHostEnvironment)sp.GetRequiredService<IHostEnvironment>());
#pragma warning restore CS0618 // Type or member is obsolete
        serviceCollection.ReplaceIt<IMemoryCache>(new FakeMemoryCache());
        serviceCollection.ReplaceIt<IUrlGenerator>(new FakeUrlGenerator());
        serviceCollection.ReplaceIt<IDataProtectionProvider>(dataProtectionProvider);
        serviceCollection.ReplaceIt<IDefaultResponderService>(new FakeDefaultResponderService());
        serviceCollection.ReplaceIt<IAnalyticsClient>(new FakeAnalyticsClient());
        serviceCollection.ReplaceIt(Substitute.For<IBotTelemetryClient>());
        serviceCollection.ReplaceIt(Substitute.For<IBotFrameworkHttpAdapter>());
        serviceCollection.ReplaceIt(new HttpClient());
        serviceCollection.ReplaceIt<ISkillRunnerClient>(new FakeSkillRunnerClient());
        serviceCollection.ReplaceIt<ISlackApiClient>(slackApiClient);
        serviceCollection.ReplaceIt(slackApiClient.Files);
        serviceCollection.ReplaceIt<IConversationsApiClient>(slackApiClient.Conversations);
        serviceCollection.ReplaceIt<IEmojiClient>(slackApiClient.Emoji);
        serviceCollection.ReplaceIt<IReactionsApiClient>(slackApiClient.Reactions);
        serviceCollection.ReplaceIt<IBackgroundJobClient>(new FakeBackgroundJobClient());
        serviceCollection.ReplaceIt<IEmojiLookup>(new FakeEmojiLookup());
        serviceCollection.ReplaceIt<IBackgroundSlackClient>(backgroundSlackClient);
        serviceCollection.ReplaceIt<IRecurringJobManager>(new FakeRecurringJobManager());
        serviceCollection.ReplaceIt<AbbotContext>(new FakeAbbotContext(), ServiceLifetime.Singleton);
        serviceCollection.ReplaceIt<IShareClient>(new FakeShareClient());
        serviceCollection.ReplaceIt<IStorage>(new MemoryStorage());
        serviceCollection.ReplaceIt<IAzureKeyVaultClient>(new FakeAzureKeyVaultClient());

        serviceCollection.Configure<SkillOptions>(o =>
            o.DataApiKey = "IRWb9gsquqVuwh5TQRLA5kX0Ih/T18zMIPt2vmnYi6+u/XK5q0kJwTVLwiuBaqsTLYUknXh1F2p9vXZ2IVUh3jpw");

        serviceCollection.ReplaceIt<IFeatureManager>(new FakeFeatureManager());

        serviceCollection.Configure<SlackOptions>(o => o.RequiredScopes = "required,scopes");

        return serviceCollection.BuildServiceProvider(true);
    }

    static IConfiguration CreateConfiguration(IEnumerable<KeyValuePair<string, string?>>? configValues)
    {
        var configBuilder = new ConfigurationBuilder();
        var baseValues = new Dictionary<string, string?>()
        {
            {"Analytics:SegmentWriteKey", "key!"},
            {"Auth0:ClientId", "Doesn't matter"},
            {"Auth0:Domain", "https://example.com"},
            {"ConnectionStrings:DbAlpha", "Host=example.com"},
            {"DataProtection:KeyVaultKeyId", "https://localhost"},
            {"DataProtection:Enabled", "true"},
            {"DataProtection:UseManagedIdentity", "true"},
            {"DataProtection:StorageAccountName", "abbotkeystest"},
            {"DataProtection:StorageContainerName", "abbot-keys-test"},
            {"DataProtection:StorageBlobName", "abbot-keys-test.xml"},
            {
                "Skill:DataApiKey",
                "IRWb9gsquqVuwh5TQRLA5kX0Ih/T18zMIPt2vmnYi6+u/XK5q0kJwTVLwiuBaqsTLYUknXh1F2p9vXZ2IVUh3jpw"
            },
            {"Eventing:Scheduler", "false"}
        };

        if (configValues is not null)
        {
            foreach (var (key, value) in configValues)
            {
                baseValues[key] = value;
            }
        }

        configBuilder.AddInMemoryCollection(baseValues);

        return configBuilder.Build();
    }
}
