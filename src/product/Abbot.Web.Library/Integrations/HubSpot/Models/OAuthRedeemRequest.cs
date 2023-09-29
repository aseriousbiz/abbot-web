using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Refit;

namespace Serious.Abbot.Integrations.HubSpot.Models;

public abstract record OAuthRedeemRequest(
    [property: AliasAs("grant_type")]
    string GrantType,
    [property: AliasAs("client_id")]
    string ClientId,
    [property: AliasAs("client_secret")]
    string ClientSecret,
    [property: AliasAs("redirect_uri")]
    string RedirectUri);

public record OAuthCodeRedeemRequest(
    [property: AliasAs("code")]
    string Code,
    string ClientId,
    string ClientSecret,
    string RedirectUri) : OAuthRedeemRequest("authorization_code", ClientId, ClientSecret, RedirectUri);

public record OAuthRefreshTokenRedeemRequest(
    [property: AliasAs("refresh_token")]
    string RefreshToken,
    string ClientId,
    string ClientSecret,
    string RedirectUri) : OAuthRedeemRequest("refresh_token", ClientId, ClientSecret, RedirectUri);

public class OAuthRedeemResponse
{
    [JsonProperty("refresh_token")]
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = null!;

    [JsonProperty("access_token")]
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonProperty("expires_in")]
    [JsonPropertyName("expires_in")]
    public int ExpiresInSeconds { get; set; }
}
