using System.Threading.Tasks;
using Refit;
using Serious.Abbot.Integrations.HubSpot.Models;

namespace Serious.Abbot.Integrations.HubSpot;

public interface IHubSpotOAuthClient
{
    [Post("/oauth/v1/token")]
    Task<OAuthRedeemResponse> RedeemCodeAsync(
        [Body(BodySerializationMethod.UrlEncoded)] OAuthRedeemRequest request);

    [Get("/oauth/v1/access-tokens/{token}")]
    Task<OAuthTokenInfo> GetTokenInfoAsync(string token);
}
