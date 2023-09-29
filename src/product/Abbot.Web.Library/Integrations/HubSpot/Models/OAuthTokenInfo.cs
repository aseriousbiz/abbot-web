using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.HubSpot.Models;

public class OAuthTokenInfo
{
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public string User { get; set; } = null!;

    [JsonProperty("hub_domain")]
    [JsonPropertyName("hub_domain")]
    public string HubDomain { get; set; } = null!;

    [JsonProperty("scopes")]
    [JsonPropertyName("scopes")]
    public IList<string> Scopes { get; set; } = null!;

    [JsonProperty("hub_id")]
    [JsonPropertyName("hub_id")]
    public int HubId { get; set; }

    [JsonProperty("app_id")]
    [JsonPropertyName("app_id")]
    public int AppId { get; set; }

    [JsonProperty("expires_in")]
    [JsonPropertyName("expires_in")]
    public int ExpiresInSeconds { get; set; }

    [JsonProperty("user_id")]
    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonProperty("token_type")]
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;
}
