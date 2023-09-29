using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack;

/// <summary>
/// Represents a request to send a message to Slack via the
/// <see href="https://api.slack.com/methods/chat.postMessage">chat.postMessage</see> endpoint.
/// </summary>
/// <param name="Channel">The channel Id to post the message to.</param>
/// <param name="Text">
/// The formatted text of the message to be published. If blocks are included, this will become the fallback
/// text used in notifications.
/// </param>
public record MessageRequest(
    [property:JsonProperty("channel")]
    [property:JsonPropertyName("channel")]
    string Channel,
    string? Text) : MessageBase(Text)
{
    /// <summary>
    /// Constructs a <see cref="MessageRequest"/>.
    /// </summary>
    public MessageRequest() : this(string.Empty, null)
    {
    }

    /// <summary>
    /// If editing an existing message, this is the timestamp of the message to edit. Otherwise leave this null.
    /// </summary>
    [JsonProperty("ts")]
    [JsonPropertyName("ts")]
    public string? Timestamp { get; set; }

    /// <summary>
    /// Provide another message's ts value to make this message a reply. Avoid using a reply's ts value;
    /// use its parent instead.
    /// </summary>
    [JsonProperty("thread_ts")]
    [JsonPropertyName("thread_ts")]
    public string? ThreadTs { get; init; }

    /// <summary>
    /// A set of blocks to be displayed in the message.
    /// </summary>
    [JsonProperty("blocks")]
    [JsonPropertyName("blocks")]
    public IReadOnlyList<ILayoutBlock>? Blocks { get; init; }

    /// <summary>
    /// The set of attachments to include in the message.
    /// </summary>
    [JsonProperty("attachments")]
    [JsonPropertyName("attachments")]
    public IReadOnlyList<LegacyMessageAttachment>? Attachments { get; init; } = new List<LegacyMessageAttachment>();

    /// <summary>
    /// Sets the icon URL for this message. Used to override the image for the user such as for Abbot.
    /// </summary>
    [JsonProperty("icon_url")]
    [JsonPropertyName("icon_url")]
    public Uri? IconUrl { get; set; }

    /// <summary>
    /// The source message that initiated the interaction form. This will include the full state of the message.
    /// </summary>
    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public object? Message { get; set; }

    /// <summary>
    /// The source view that initiated the interaction form. This will include the full state of the view within
    /// a modal or Home tab.
    /// </summary>
    [JsonProperty("view")]
    [JsonPropertyName("view")]
    public object? View { get; set; }

    /// <summary>
    /// Gets or sets metadata to attach to the message.
    /// </summary>
    [JsonProperty("metadata")]
    [JsonPropertyName("metadata")]
    public MessageMetadata? Metadata { get; set; }

    /// <summary>
    /// If <see cref="ThreadTs"/> is set and this is <c>true</c>, the reply will be broadcast to the entire channel.
    /// This is equivalent to the "Also send to #channel" checkbox in the Slack thread reply UI.
    /// </summary>
    [JsonProperty("reply_broadcast")]
    [JsonPropertyName("reply_broadcast")]
    public bool ReplyBroadcast { get; set; }
}

/// <summary>
/// Represents metadata to attach to a Slack message.
/// See https://api.slack.com/metadata for more information.
/// </summary>
public class MessageMetadata
{
    /// <summary>
    /// The type of the event represented by this message metadata.
    /// </summary>
    [JsonProperty("event_type")]
    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }

    /// <summary>
    /// The payload associated with the event.
    /// </summary>
    [JsonProperty("event_payload")]
    [JsonPropertyName("event_payload")]
    public IDictionary<string, object?> EventPayload { get; set; } = new Dictionary<string, object?>();
}

/// <summary>
/// Represents a request to send an ephemeral message to a user in Slack via the
/// <see href="https://api.slack.com/methods/chat.postEphemeral">chat.postEphemeral</see> endpoint.
/// </summary>
public record EphemeralMessageRequest : MessageRequest
{
    /// <summary>
    /// Constructs an <see cref="EphemeralMessageRequest"/> using the specified <see cref="MessageRequest"/> as a
    /// template.
    /// </summary>
    /// <param name="messageRequest">The request body to create a message.</param>
    public EphemeralMessageRequest(MessageRequest messageRequest) : base(messageRequest)
    {
    }

    /// <summary>
    /// The Slack user id of the user who will receive the ephemeral message. The user should be in the channel
    /// specified by the channel argument. Default is <c>null</c>.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public required string User { get; init; } = null!;
}
