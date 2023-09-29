using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The response to calling <c>chat.postMessage</c> or <c>chat.update</c>.
/// </summary>
public class MessageResponse : InfoResponse<MessageBody>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The posted message, with extra information such as the message's timestamp.
    /// </summary>
    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public override MessageBody? Body { get; init; }

    /// <summary>
    /// The unique (per-channel) timestamp for the message.
    /// </summary>
    [JsonProperty("ts")]
    [JsonPropertyName("ts")]
    public string Timestamp { get; set; } = null!;

    /// <summary>
    /// The channel where this message was posted.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string Channel { get; init; } = null!;
}

/// <summary>
/// The response to calling <c>chat.postEphemeral</c>.
/// </summary>
public class EphemeralMessageResponse : InfoResponse<string>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The timestamp of the message
    /// </summary>
    [JsonProperty("message_ts")]
    [JsonPropertyName("message_ts")]
    public override string? Body { get; init; }

}
