using Microsoft.Extensions.Configuration;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.MergeDev;

namespace Microsoft.Extensions.DependencyInjection;

public static class MergeDevStartupExtensions
{
    public static void AddMergeDevIntegrationServices(this IServiceCollection services, IConfiguration hubSpotConfigSection)
    {
        services.Configure<MergeDevOptions>(hubSpotConfigSection);
        services.AddSingleton<IMergeDevClientFactory, MergeDevClientFactory>();
        services.AddScoped<ITicketLinker<TicketingSettings>, MergeDevLinker>();
    }
}
