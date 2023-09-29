using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Icons for the bot.
/// </summary>
/// <param name="Image36">The 36x36 icon.</param>
/// <param name="Image48">The 48x48 icon.</param>
/// <param name="Image72">The 72x72 icon.</param>
public record BotIcons(
[property: JsonProperty("image_36")]
    [property: JsonPropertyName("image_36")]
    string? Image36,

    [property: JsonProperty("image_48")]
    [property: JsonPropertyName("image_48")]
    string? Image48,

    [property: JsonProperty("image_72")]
    [property: JsonPropertyName("image_72")]
    string? Image72);
