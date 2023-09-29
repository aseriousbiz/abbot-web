using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Octokit;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Cryptography;
using ProductHeaderValue = Octokit.ProductHeaderValue;

namespace Serious.Abbot.Integrations.GitHub;

public interface IGitHubClientFactory
{
    HttpRequestMessage ApplyAuthorization(HttpRequestMessage request, string apiToken);

    IGitHubClient CreateAnonymousClient();

    IGitHubClient CreateAppClient();

    Task<SecretString> GetOrRenewAccessTokenAsync(Integration integration, GitHubSettings settings);

    Task<IGitHubClient> CreateInstallationClientAsync(Integration integration, GitHubSettings settings);
}

public class GitHubClientFactory : IGitHubClientFactory
{
    public static readonly string UserAgentProductName = "Serious.Abbot.GitHub";

    readonly GitHubJwtFactory _jwtFactory;
    readonly IIntegrationRepository _integrationRepository;
    readonly IClock _clock;
    readonly IDataProtectionProvider _dataProtectionProvider;

    public GitHubClientFactory(
        GitHubJwtFactory jwtFactory,
        IIntegrationRepository integrationRepository,
        IClock clock,
        IDataProtectionProvider dataProtectionProvider)
    {
        _jwtFactory = jwtFactory;
        _integrationRepository = integrationRepository;
        _clock = clock;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public IGitHubClient CreateAnonymousClient() => CreateClient();

    public IGitHubClient CreateAppClient()
    {
        var appClient = CreateClient();
        appClient.Credentials = new Credentials(_jwtFactory.GenerateJwt(), AuthenticationType.Bearer);
        return appClient;
    }

    public async Task<SecretString> GetOrRenewAccessTokenAsync(Integration integration, GitHubSettings settings)
    {
        var installationId = settings.InstallationId.Require("GitHub application is improperly configured. Installation ID is missing.");

        if (settings.InstallationToken is not { Empty: false }
            || !(settings.InstallationTokenExpiryUtc > _clock.UtcNow + TimeSpan.FromMinutes(5)))
        {
            // We need a fresh installation token.
            var appClient = CreateAppClient();
            var installationToken = await appClient.GitHubApps.CreateInstallationToken(installationId);
            settings.InstallationToken = new SecretString(installationToken.Token, _dataProtectionProvider);
            settings.InstallationTokenExpiryUtc = installationToken.ExpiresAt.UtcDateTime;
            await _integrationRepository.SaveSettingsAsync(integration, settings);
        }

        return settings.InstallationToken;
    }

    public async Task<IGitHubClient> CreateInstallationClientAsync(Integration integration, GitHubSettings settings)
    {
        var apiToken = await GetOrRenewAccessTokenAsync(integration, settings);

        var client = CreateClient();
        client.Credentials = new Credentials(apiToken.Reveal());
        return client;
    }

    public HttpRequestMessage ApplyAuthorization(HttpRequestMessage request, string apiToken)
    {
        var headers = request.Headers;
        SetCommonHeaders(headers);
        headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiToken);
        return request;
    }

    static readonly AssemblyBuildMetadata BuildMetadata = typeof(IGitHubClientFactory).Assembly.GetBuildMetadata();
    static void SetCommonHeaders(HttpRequestHeaders requestHeaders)
    {
        requestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProductName, BuildMetadata.InformationalVersion));
    }

    static GitHubClient CreateClient() => new(new ProductHeaderValue(UserAgentProductName, BuildMetadata.InformationalVersion));
}
