using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serious.Abbot.Conversations;
using Serious.Abbot.Infrastructure.Http;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.HubSpot;

namespace Microsoft.Extensions.DependencyInjection;

public static class HubSpotStartupExtensions
{
    public static void AddHubSpotIntegrationServices(this IServiceCollection services, IConfiguration hubSpotConfigSection)
    {
        services.Configure<HubSpotOptions>(hubSpotConfigSection);
        services.AddScoped<IHubSpotClientFactory, HubSpotClientFactory>();
        services.TryAddTransient<RequestLoggingHttpMessageHandler>();
        services.AddHttpClient("HubSpot").AddHttpMessageHandler<RequestLoggingHttpMessageHandler>();
        services.AddTransient<IConversationListener, HubSpotConversationListener>();
        services.AddTransient<HubSpotFormatter>();
        services.AddScoped<ITicketLinker<HubSpotSettings>, HubSpotLinker>();
        services.AddTransient<IHubSpotLinker, HubSpotLinker>();
        services.AddTransient<IUserIdentityLinker, HubSpotUserIdentityLinker>();
        services.AddTransient<IHubSpotResolver, HubSpotResolver>();
        services.AddSingleton<HubSpotWebhookSignatureVerificationFilter>();
    }
}
