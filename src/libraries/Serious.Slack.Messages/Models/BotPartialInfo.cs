using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Partial information about a Slack Bot User. This is what we get during an installation "bot_added"
/// event.
/// </summary>
public record BotPartialInfo : EntityBase
{
    /// <summary>
    /// The name of the bot.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// If the bot belongs to a Slack app, the <see cref="AppId"/> points to its parent app.
    /// </summary>
    [JsonProperty("app_id")]
    [JsonPropertyName("app_id")]
    public string? AppId { get; set; }

    /// <summary>
    /// Whether the bot is deleted or not.
    /// </summary>
    [JsonProperty("deleted")]
    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}
