using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Abbot.Repositories;
using Serious.Cryptography;

namespace Serious.Abbot.Integrations.HubSpot;

public interface IHubSpotClientFactory
{
    /// <summary>
    /// Gets the access token for the provided settings.
    /// If the access token in <paramref name="settings"/> has expired, it will be renewed using the refresh token.
    /// The settings will be automatically saved after renewing the token.
    /// </summary>
    /// <param name="integration">The <see cref="Integration"/> representing the HubSpot integration to get the client for.</param>
    /// <param name="settings">The <see cref="HubSpotSettings"/> to use to connect to HubSpot.</param>
    /// <returns>A <see cref="SecretString"/> with the token that can be used to interact with the HubSpot API.</returns>
    Task<SecretString> GetOrRenewAccessTokenAsync(Integration integration, HubSpotSettings settings);

    IHubSpotOAuthClient CreateOAuthClient();

    /// <summary>
    /// Creates a <see cref="IHubSpotClient"/> for the provided settings.
    /// If the access token in <paramref name="settings"/> has expired, it will be renewed using the refresh token.
    /// The settings will be automatically saved after renewing the token.
    /// </summary>
    /// <param name="integration">The <see cref="Integration"/> representing the HubSpot integration to get the client for.</param>
    /// <param name="settings">The <see cref="HubSpotSettings"/> to use to connect to HubSpot.</param>
    /// <returns>A <see cref="IHubSpotClient"/> that can be used to interact with the HubSpot API.</returns>
    Task<IHubSpotClient> CreateClientAsync(Integration integration, HubSpotSettings settings);

    /// <summary>
    /// Creates a <see cref="IHubSpotFormsClient"/> for the provided settings.
    /// If the access token in <paramref name="settings"/> has expired, it will be renewed using the refresh token.
    /// The settings will be automatically saved after renewing the token.
    /// </summary>
    /// <param name="integration">The <see cref="Integration"/> representing the HubSpot integration to get the client for.</param>
    /// <param name="settings">The <see cref="HubSpotSettings"/> to use to connect to HubSpot.</param>
    /// <returns>A <see cref="IHubSpotClient"/> that can be used to interact with the HubSpot API.</returns>
    Task<IHubSpotFormsClient> CreateFormsClientAsync(Integration integration, HubSpotSettings settings);
}

public class HubSpotClientFactory : IHubSpotClientFactory
{
    public static readonly string UserAgentProductName = "Serious.Abbot.HubSpot";

    static readonly RefitSettings RefitSettings = new()
    {
        ContentSerializer = new NewtonsoftJsonContentSerializer(new()
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
        }),
    };

    readonly IHttpClientFactory _httpClientFactory;
    readonly IClock _clock;
    readonly IOptions<HubSpotOptions> _hubSpotOptions;
    readonly IDataProtectionProvider _dataProtectionProvider;
    readonly IIntegrationRepository _integrationRepository;

    public HubSpotClientFactory(IHttpClientFactory httpClientFactory, IClock clock, IOptions<HubSpotOptions> hubSpotOptions, IDataProtectionProvider dataProtectionProvider, IIntegrationRepository integrationRepository)
    {
        _httpClientFactory = httpClientFactory;
        _clock = clock;
        _hubSpotOptions = hubSpotOptions;
        _dataProtectionProvider = dataProtectionProvider;
        _integrationRepository = integrationRepository;
    }

    public async Task<SecretString> GetOrRenewAccessTokenAsync(Integration integration, HubSpotSettings settings)
    {
        Expect.True(settings.HasApiCredentials);

        if (settings.AccessTokenExpiryUtc - _clock.UtcNow < TimeSpan.FromMinutes(5))
        {
            // The access token is about to expire, so we need to renew it.
            var oauthClient = CreateOAuthClient();
            var redeemResponse = await oauthClient.RedeemCodeAsync(new OAuthRefreshTokenRedeemRequest(
                settings.RefreshToken.Reveal(),
                _hubSpotOptions.Value.ClientId.Require("Required setting 'HubSpot:ClientId' is missing"),
                _hubSpotOptions.Value.ClientSecret.Require("Required setting 'HubSpot:ClientSecret' is missing"),
                settings.RedirectUri));
            settings.AccessToken = new SecretString(redeemResponse.AccessToken, _dataProtectionProvider);
            settings.RefreshToken = new SecretString(redeemResponse.RefreshToken, _dataProtectionProvider);
            settings.AccessTokenExpiryUtc = _clock.UtcNow.AddSeconds(redeemResponse.ExpiresInSeconds);
            await _integrationRepository.SaveSettingsAsync(integration, settings);
        }

        return settings.AccessToken;
    }

    public IHubSpotOAuthClient CreateOAuthClient()
    {
        var httpClient = _httpClientFactory.CreateClient("HubSpot");
        httpClient.BaseAddress = IHubSpotClient.ApiUrl;
        SetCommonHeaders(httpClient);

        return RestService.For<IHubSpotOAuthClient>(httpClient);
    }

    public async Task<IHubSpotClient> CreateClientAsync(Integration integration, HubSpotSettings settings)
    {
        var accessToken = await GetOrRenewAccessTokenAsync(integration, settings);

        var httpClient = _httpClientFactory.CreateClient("HubSpot");
        httpClient.BaseAddress = IHubSpotClient.ApiUrl;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Reveal());
        SetCommonHeaders(httpClient);

        return RestService.For<IHubSpotClient>(httpClient, RefitSettings);
    }

    public async Task<IHubSpotFormsClient> CreateFormsClientAsync(Integration integration, HubSpotSettings settings)
    {
        var accessToken = await GetOrRenewAccessTokenAsync(integration, settings);

        var httpClient = _httpClientFactory.CreateClient("HubSpot");
        httpClient.BaseAddress = IHubSpotFormsClient.ApiUrl;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Reveal());
        SetCommonHeaders(httpClient);

        return RestService.For<IHubSpotFormsClient>(httpClient, RefitSettings);
    }

    static readonly AssemblyBuildMetadata BuildMetadata = typeof(IHubSpotClientFactory).Assembly.GetBuildMetadata();
    static void SetCommonHeaders(HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProductName, BuildMetadata.InformationalVersion));
    }
}
