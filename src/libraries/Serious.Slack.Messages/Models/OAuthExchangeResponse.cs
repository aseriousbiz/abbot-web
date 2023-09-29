using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The response from the Slack API when exchanging an OAuth code.
/// </summary>
public class OAuthExchangeResponse : ApiResponse
{
    /// <summary>
    /// The access token for the bot.
    /// </summary>
    [JsonProperty("access_token")]
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    /// <summary>
    /// The type of the access token.
    /// </summary>
    [JsonProperty("token_type")]
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;

    /// <summary>
    /// The scopes granted to the bot.
    /// </summary>
    [JsonProperty("scope")]
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = null!;

    /// <summary>
    /// The Slack User ID for the bot.
    /// </summary>
    [JsonProperty("bot_user_id")]
    [JsonPropertyName("bot_user_id")]
    public string BotUserId { get; set; } = null!;

    /// <summary>
    /// The Slack App ID for the bot.
    /// </summary>
    [JsonProperty("app_id")]
    [JsonPropertyName("app_id")]
    public string AppId { get; set; } = null!;

    /// <summary>
    /// The Slack Team in which the bot is installed.
    /// </summary>
    [JsonProperty("team")]
    [JsonPropertyName("team")]
    public TeamInfo Team { get; set; } = null!;
}
