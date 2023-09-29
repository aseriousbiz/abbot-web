using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Serious.Abbot.Integrations.GitHub;

public static class GitHubStartupExtensions
{
    public static void AddGitHubIntegrationServices(this IServiceCollection services, IConfiguration githubConfigSection)
    {
        services.Configure<GitHubOptions>(githubConfigSection);
        services.AddSingleton<GitHubJwtFactory>();
        services.AddScoped<IGitHubClientFactory, GitHubClientFactory>();
        services.AddScoped<ITicketLinker<GitHubSettings>, GitHubLinker>();
    }
}

public class GitHubOptions
{
    public string? AppId { get; set; }
    public string? AppName { get; set; }
    public string? AppSlug { get; set; }
    public string? AppKey { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
