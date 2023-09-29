using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack;

/// <summary>
/// The response from the Slack API when retrieving message history for a channel.
/// </summary>
public class ConversationHistoryResponse : InfoResponse<IReadOnlyList<SlackMessage>>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The messages.
    /// </summary>
    [JsonProperty("messages")]
    [JsonPropertyName("messages")]
    public override IReadOnlyList<SlackMessage>? Body { get; init; }

    /// <summary>
    /// Whether or not there are more messages.
    /// </summary>
    [JsonProperty("has_more")]
    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }

    /// <summary>
    /// The number of pins.
    /// </summary>
    [JsonProperty("pin_count")]
    [JsonPropertyName("pin_count")]
    public int PinCount { get; init; }
}
