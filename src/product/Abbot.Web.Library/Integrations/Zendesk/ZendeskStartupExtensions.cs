using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serious.Abbot.Conversations;
using Serious.Abbot.Infrastructure.Http;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.Zendesk;

namespace Microsoft.Extensions.DependencyInjection;

public static class ZendeskStartupExtensions
{
    public static void AddZendeskIntegrationServices(this IServiceCollection services, IConfiguration zendeskConfigSection)
    {
        services.AddSingleton<IZendeskClientFactory, ZendeskClientFactory>();
        services.TryAddTransient<RequestLoggingHttpMessageHandler>();
        services.AddHttpClient("Zendesk").AddHttpMessageHandler<RequestLoggingHttpMessageHandler>();
        services.AddScoped<ITicketLinker<ZendeskSettings>, ZendeskLinker>();
        services.AddTransient<IUserIdentityLinker, ZendeskUserIdentityLinker>();
        services.AddTransient<IZendeskResolver, ZendeskResolver>();
        services.AddTransient<IZendeskInstaller, ZendeskInstaller>();
        services.AddTransient<IZendeskToSlackImporter, ZendeskToSlackImporter>();
        services.AddTransient<ISlackToZendeskCommentImporter, SlackToZendeskConversationListener>();
        services.AddTransient<ZendeskFormatter>();

        services.AddTransient<IConversationListener, SlackToZendeskConversationListener>();
        services.Configure<ZendeskOptions>(zendeskConfigSection);
    }
}
