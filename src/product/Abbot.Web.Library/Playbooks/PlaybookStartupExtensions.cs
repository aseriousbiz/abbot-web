using Microsoft.Extensions.Hosting;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlaybookStartupExtensions
{
    public static void AddPlaybookServices(this IServiceCollection services)
    {
        services.AddScoped<StepTypeCatalog>();
        services.AddScoped<PlaybookDispatcher>();
        services.AddScoped<PlaybookPublisher>();

        services.AddSingleton<ActionDispatcher>();
        services.RegisterAllTypesInSameAssembly<ITriggerType>(publicOnly: true, lifetime: ServiceLifetime.Singleton);
        services.RegisterAllTypesInSameAssembly<IActionType>(ServiceLifetime.Singleton);
    }
}
