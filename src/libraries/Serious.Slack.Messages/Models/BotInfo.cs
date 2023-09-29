using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Information about a Slack Bot User as part of the response from the <c>bots.info</c> endpoint.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/methods/bots.info" /> for more info.
/// </remarks>
public record BotInfo : BotPartialInfo
{
    /// <summary>
    /// The User Id for the bot.
    /// </summary>
    [JsonProperty("user_id")]
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// The set of icons for the bot.
    /// </summary>
    [JsonProperty("icons")]
    [JsonPropertyName("icons")]
    public BotIcons Icons { get; set; } = null!;
}
