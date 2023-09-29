using System;
using System.Linq;
using System.Threading.Tasks;
using Auth0.ManagementApi;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;

namespace Serious.Abbot.Clients;

/// <summary>
/// Class used to retrieve an identity provider token from the current logged in user.
/// </summary>
public class IdentityProviderTokenRetriever : IIdentityProviderTokenRetriever
{
    readonly IApiTokenCache _apiTokenCache;
    readonly Auth0Options _options;

    public IdentityProviderTokenRetriever(IApiTokenCache apiTokenCache, IOptions<Auth0Options> options)
    {
        _apiTokenCache = apiTokenCache;
        _options = options.Value;
    }

    /// <summary>
    /// Attempts to call the Auth0 Management API to retrieve the access token for the user for the
    /// specified connection.
    /// </summary>
    /// <param name="nameIdentifier">The name identifier claim.</param>
    /// <param name="connection">The Auth0 connection such as "slack", "discord", or "azure-ad"</param>
    /// <returns></returns>
    public async Task<string?> GetLoggedInUserAccessTokenAsync(string nameIdentifier, string connection)
    {
        var managementApiToken = await _apiTokenCache.GetAsync(
            ApiIdentifier.Auth0ManagementApi,
            TimeSpan.FromMinutes(5));

        string domain = _options.Domain
                        ?? throw new InvalidOperationException("\"Auth0:Domain\" Not set");

        using var auth0Client = new ManagementApiClient(managementApiToken, domain);
        var user = await auth0Client.Users.GetAsync(nameIdentifier);
        var identity = user.Identities.FirstOrDefault(i => i.Connection.Equals(connection, StringComparison.Ordinal));
        return identity?.AccessToken;
    }
}
