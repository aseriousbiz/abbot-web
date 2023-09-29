using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class OAuthTokenMessage
{
    [JsonProperty("access_token")]
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonProperty("token_type")]
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;

    [JsonProperty("scope")]
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = null!;
}
