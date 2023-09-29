using System;
using System.Threading.Tasks;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Tasks;

namespace Serious.Abbot.Clients;

/// <summary>
/// Retrieves a cached api token.
/// </summary>
public class ApiTokenCache : IApiTokenCache
{
    readonly AsyncAgedCache<ApiIdentifier, string> _cache;

    readonly Auth0Options _options;

    public ApiTokenCache(IOptions<Auth0Options> options)
    {
        _options = options.Value;
        _cache = new AsyncAgedCache<ApiIdentifier, string>(GetApiToken);
    }

    public Task<string> GetAsync(ApiIdentifier apiIdentifier, TimeSpan maxAge)
    {
        return _cache.GetOrAddAsync(apiIdentifier, maxAge);
    }

    Task<string> GetApiToken(ApiIdentifier apiIdentifier)
    {
        return apiIdentifier switch
        {
            ApiIdentifier.Auth0ManagementApi => GetManagementApiToken(),
            _ => throw new ArgumentException("Unknown Api Token identifier", nameof(apiIdentifier))
        };
    }

    async Task<string> GetManagementApiToken()
    {
        string domain = _options.Domain
                        ?? throw new InvalidOperationException("\"Auth0:Domain\" Not set");
        string clientId = _options.ClientId
                          ?? throw new InvalidOperationException("\"Auth0:ClientId\" Not set");
        string clientSecret = _options.ClientSecret
                              ?? throw new InvalidOperationException("\"Auth0:ClientSecret\" Not set");
        using var client = new AuthenticationApiClient(domain);
        var token = await client.GetTokenAsync(new ClientCredentialsTokenRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Audience = $"https://{domain}/api/v2/"
        });
        return token.AccessToken;
    }
}

public enum ApiIdentifier
{
    Auth0ManagementApi
}
