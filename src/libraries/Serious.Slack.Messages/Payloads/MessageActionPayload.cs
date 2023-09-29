using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack;
using Serious.Slack.Converters;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;

namespace Serious.Payloads;

/// <summary>
/// The event raised when a <see href="https://api.slack.com/interactivity/shortcuts/using#getting_started">Message Shortcut</see> is invoked.
/// </summary>
[Element("message_action")]
public record MessageActionPayload() : InteractionPayload("message_action"), IMessagePayload<SlackMessage>
{
    /// <summary>
    /// The timestamp for when the user interacted with this element.
    /// </summary>
    [JsonProperty("action_ts")]
    [JsonPropertyName("action_ts")]
    public string? ActionTimestamp { get; init; }

    /// <summary>
    /// The message that the action was invoked on.
    /// </summary>
    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public SlackMessage Message { get; set; } = null!;

    /// <summary>
    /// The string you provided as the <c>callback_id</c> when configuring the Message Action.
    /// </summary>
    [JsonProperty("callback_id")]
    [JsonPropertyName("callback_id")]
    public string CallbackId { get; init; } = null!;

    /// <summary>
    /// string	The time when the message containing the action was posted, expressed in decimal epoch time,
    /// wrapped in a string. Like <c>"1458170917.164398"</c>.
    /// </summary>
    [JsonProperty("message_ts")]
    [JsonPropertyName("message_ts")]
    public string MessageTimestamp { get; init; } = null!;

    /// <summary>
    /// A short-lived webhook that can be used to
    /// <see href="https://api.slack.com/interactivity/handling#message_responses">send messages</see> in
    /// response to this message action.
    /// </summary>
    [JsonProperty("response_url")]
    [JsonPropertyName("response_url")]
    public Uri ResponseUrl { get; init; } = null!;

    /// <summary>
    /// Information about the channel containing the message this action was invoked on.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public ChannelInfo Channel { get; set; } = null!;
}
