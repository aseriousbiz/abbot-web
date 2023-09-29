using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serious.Abbot.Execution;
using Serious.Abbot.Functions.Cache;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Scripting;
using Serious.Abbot.Scripting.Utilities;
using Serious.Abbot.Storage;

[assembly: CLSCompliant(false)]
namespace Serious.Abbot.Functions.DotNet;

public static class Startup
{
    public static void RegisterAbbotServices(this IServiceCollection services)
    {
        services.AddHttpClient();

        // Read only assembly cache
        services.AddTransient<ICompilationCache, CompilationCache>();

        services.AddSingleton<IOptions<MemoryCacheOptions>>(new MemoryCacheOptions
        {
            SizeLimit = 1000
        });
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IEnvironment, SystemEnvironment>();
        services.AddHttpClient<IBotHttpClient, BotHttpClient>();
        services.AddHttpClient<ISkillApiClient, SkillApiClient>();
        services.AddTransient<ISecrets, BotSecrets>();
        services.AddTransient<ITasksClient, TasksClient>();
        services.AddTransient<ITicketsClient, TicketsClient>();
        services.AddTransient<IMetadataClient, MetadataClient>();
        services.AddTransient<ISlack, SlackClient>();
        services.AddTransient<IRoomsClient, RoomsClient>();
        services.AddTransient<ICustomersClient, CustomersClient>();
        services.AddTransient<IUsersClient, UsersClient>();
        services.AddTransient<IUtilities, BotUtilities>();
        services.AddTransient<ActiveBotReplyClient>();
        services.AddScoped<IBotReplyClient, BotReplyClient>();
        services.AddTransient<ICompiledSkillRunner, CompiledSkillRunner>();
        services.AddTransient<IBrainSerializer, BrainSerializer>();
        services.AddTransient<IExtendedBrain, BotBrain>();
        services.AddTransient<IBrainApiClient, BrainApiClient>();
        services.AddTransient<IExtendedBot, AbbotBot>();
        services.AddTransient<ISignaler, Signaler>();

        // This HAS TO BE SCOPED!
        services.AddScoped<ISkillContextAccessor, SkillContextAccessor>();
    }
}
