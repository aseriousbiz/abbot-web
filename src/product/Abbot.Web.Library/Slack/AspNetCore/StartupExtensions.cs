using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Serious.Slack.AspNetCore;

/// <summary>
/// Extension methods to help register the services needed for the Slack integration.
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    /// Registers the <see cref="SlackRequestVerificationFilter"/> as a singleton.
    /// Also configures the <see cref="SlackOptions"/> so they can be injected into the
    /// default <see cref="ISlackOptionsProvider"/> (<see cref="DefaultSlackOptionsProvider"/>).
    /// </summary>
    /// <param name="services">An <see cref="IServiceCollection"/> instance.</param>
    /// <param name="configuration">An <see cref="IConfiguration"/> instance.</param>
    /// <param name="slackSectionName">
    /// The name of the Configuration section with the Slack settings
    /// matching the properties of <see cref="SlackOptions"/>. Defaults to "Slack".
    /// </param>
    public static void AddSlackRequestVerificationFilter(
        this IServiceCollection services,
        IConfiguration configuration,
        string slackSectionName = "Slack")
    {
        // Using TryAdd to ensure we don't override an existing registration,
        // e.g. a scoped ISlackOptionsProvider requires a scoped filter, too
        services.TryAddSingleton<SlackRequestVerificationFilter>();
        services.Configure<SlackOptions>(configuration.GetSection(slackSectionName));
        services.TryAddSingleton<ISlackOptionsProvider, DefaultSlackOptionsProvider>();
    }

    /// <summary>
    /// Adds <see cref="SlackRequestVerificationFilter"/> to the set of filters to apply to the MVC pipeline.
    /// </summary>
    /// <param name="filterCollection"></param>
    public static void AddSlackRequestVerificationFilter(this FilterCollection filterCollection)
    {
        filterCollection.Add<SlackRequestVerificationFilter>();
    }
}
