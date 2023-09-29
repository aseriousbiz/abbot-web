using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serious.Abbot.Api;
using Serious.Abbot.Clients;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Slack;

namespace Serious.Abbot.AppStartup;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a set of services for the Abbot API.
    /// </summary>
    /// <param name="services"></param>
    public static void AddApiServices(this IServiceCollection services)
    {
        services.AddTransient<ApiService>();
        services.AddTransient<InsightsApiService>();
        services.AddTransient<CustomerApiService>();
        services.AddTransient<TasksApiService>();
    }

    /// <summary>
    /// Registers the services needed for all Abbot's Repositories and the <see cref="AbbotContext" /> used to
    /// query the database.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The <see cref="IConfiguration"/> to retrieve the connection string from.</param>
    public static void AddDatabaseAndRepositoryServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(AbbotContext.ConnectionStringName);
        if (connectionString is null)
        {
            throw new InvalidOperationException(
                $"The `ConnectionStrings:DbAlpha` setting is not configured in the `ConnectionStrings`" +
                $" section. For local development, make sure `ConnectionStrings:DbAlpha` is set properly " +
                "in `appsettings.Development.json` within `Abbot.Web`.");
        }

        // See if we need to rewrite the connection string
        var databaseOptions = new DatabaseOptions();
        configuration.GetSection("Databases:Alpha").Bind(databaseOptions);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (databaseOptions.OverridePort is not null)
        {
            builder.Port = databaseOptions.OverridePort.Value;
        }

        services.AddDatabaseContexts(builder.ConnectionString);
        services.AddSlackApiClient<CachingCustomEmojiLookup>();
        services.AddTransient<IBackgroundSlackClient, BackgroundSlackClient>();
        services.AddTransient<IScheduledSkillClient, ScheduledSkillClient>();
        services.AddScoped<IRunnerEndpointManager, RunnerEndpointManager>();

        services.AddRepositories();
    }

    /// <summary>
    /// Adds database repositories
    /// </summary>
    /// <param name="services"></param>
    public static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<CustomerRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IPackageRepository, PackageRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IMetadataRepository, MetadataRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<IPatternRepository, PatternRepository>();
        services.AddScoped<ISignalRepository, SignalRepository>();
        services.AddScoped<ITriggerRepository, TriggerRepository>();
        services.AddScoped<IListRepository, ListRepository>();
        services.AddScoped<IAliasRepository, AliasRepository>();
        services.AddSingleton<IAnnouncementCache, AnnouncementCache>();
        services.AddScoped<IAnnouncementsRepository, AnnouncementsRepository>();
        services.AddScoped<IAnnouncementScheduler, AnnouncementScheduler>();
        services.AddScoped<IMemoryRepository, MemoryRepository>();
        services.AddScoped<IMemberFactRepository, MemberFactRepository>();
        services.AddScoped<ISettingsManager, SettingsManager>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IInsightsRepository, InsightsRepository>();
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<ILinkedIdentityRepository, LinkedIdentityRepository>();
        services.AddScoped<IFormsRepository, FormsRepository>();
        services.AddScoped<IHubRepository, HubRepository>();
        services.AddScoped<TaskRepository>();
        services.AddScoped<PlaybookRepository>();
        services.AddScoped<NotificationRepository>();
    }

    static void AddDatabaseContexts(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<AbbotContext>(
            options => SetupAbbotContextOptions(connectionString, options));
        services.AddDbContext<AbbotContext>(
            options => SetupAbbotContextOptions(connectionString, options),
            optionsLifetime: ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Setup a DbContextOptions object for a requested `DbContext` subclass.
    /// </summary>
    /// <param name="connectionString">The connection string to use for the DbContext.</param>
    /// <param name="options"></param>
    /// <exception cref="InvalidOperationException"></exception>
    static void SetupAbbotContextOptions(string connectionString, DbContextOptionsBuilder options)
    {
#if DEBUG
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
#endif
        options.ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NavigationBaseIncludeIgnored));

        options.UseNpgsql(connectionString,
            o => {
                o.MigrationsAssembly(WebConstants.MigrationsAssembly);
                o.UseNetTopologySuite();
            });
    }
}
