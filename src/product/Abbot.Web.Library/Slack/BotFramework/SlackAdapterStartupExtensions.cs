using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serious.Slack.AspNetCore;

namespace Serious.Slack.BotFramework;

public static class SlackAdapterStartupExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="SlackAdapter"/> and its dependencies.
    /// </summary>
    /// <param name="services">An <see cref="IServiceCollection"/> instance.</param>
    /// <param name="configuration">An <see cref="IConfiguration"/> instance.</param>
    public static void AddSlackAdapterServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSlackRequestVerificationFilter(configuration);
    }
}
