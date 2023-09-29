using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Event raised when a bot is added to a workspace. See <see href="https://api.slack.com/events/bot_added" /> for
/// more info.
/// </summary>
/// <remarks>
/// This event is special as it doesn't match the format of other Slack events. For example, the event payload
/// doesn't have a type.
/// </remarks>
[Element("bot_added")]
public sealed record BotAddedEvent() : Element("bot_added"), IElement
{
    /// <summary>
    /// Information about the bot that was added.
    /// </summary>
    [JsonProperty("bot")]
    [JsonPropertyName("bot")]
    public BotPartialInfo Bot { get; init; } = null!;

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    string? IElement.ApiAppId => Bot.AppId;
}
