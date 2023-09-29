using System.Threading.Tasks;

namespace Serious.Abbot.Clients;

public interface IIdentityProviderTokenRetriever
{
    /// <summary>
    /// Attempts to call the Auth0 Management API to retrieve the access token for the user for the
    /// specified connection.
    /// </summary>
    /// <param name="nameIdentifier">The name identifier claim.</param>
    /// <param name="connection">The Auth0 connection such as "slack", "discord", or "azure-ad"</param>
    /// <returns></returns>
    Task<string?> GetLoggedInUserAccessTokenAsync(string nameIdentifier, string connection);
}
