using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The response from the Slack API when testing authentication.
/// </summary>
public class AuthTestResponse : ApiResponse
{
    /// <summary>
    /// The URL of the team associated with the token.
    /// </summary>
    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// The name of the team associated with the token.
    /// </summary>
    [JsonProperty("team")]
    [JsonPropertyName("team")]
    public string? Team { get; set; }

    /// <summary>
    /// The name of the user associated with the token.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public string? User { get; set; }

    /// <summary>
    /// The ID of the team associated with the token.
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string? TeamId { get; set; }

    /// <summary>
    /// The ID of the user associated with the token.
    /// </summary>
    [JsonProperty("user_id")]
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    /// <summary>
    /// The ID of the bot associated with the token.
    /// </summary>
    [JsonProperty("bot_id")]
    [JsonPropertyName("bot_id")]
    public string? BotId { get; set; }

    /// <summary>
    /// When <c>true</c>, the Slack Team is an enterprise install.
    /// </summary>
    [JsonProperty("is_enterprise_install")]
    [JsonPropertyName("is_enterprise_install")]
    public bool IsEnterpriseInstall { get; set; }
}

/// <summary>
/// The response from the slack API when we need to also retrieve the scopes.
/// </summary>
/// <param name="ApiResponse">The response type.</param>
/// <param name="Scopes">The Slack scopes retrieved from the headers.</param>
/// <typeparam name="T"></typeparam>
public record ApiResponseWithScopes<T>(T ApiResponse, string Scopes) where T : ApiResponse;
