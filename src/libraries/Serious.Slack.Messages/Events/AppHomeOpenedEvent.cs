using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// The event raised in Slack when someone opens the App Home page which triggers the <c>app_home_opened</c> event.
/// <see href="https://api.slack.com/events/app_home_opened"/>.
/// </summary>
/// <remarks>
/// What we get in practice seems to differ from the documentation.
/// </remarks>
[Element("app_home_opened")]
public record AppHomeOpenedEvent() : EventBody("app_home_opened")
{
    /// <summary>
    /// The tab that was opened. Typically <c>home</c>.
    /// </summary>
    [JsonProperty("tab")]
    [JsonPropertyName("tab")]
    public string Tab { get; init; } = null!;

    /// <summary>
    /// The channel where this event occurred.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string? Channel { get; init; }
}
