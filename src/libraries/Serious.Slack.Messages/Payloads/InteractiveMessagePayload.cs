using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;
using Serious.Slack.Payloads;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// Represents an Interactive Message payload. This is a deprecated feature of Slack, but we use it right now when
/// we send a message with buttons.
/// https://api.slack.com/legacy/interactive-message-field-guide
/// </summary>
[Element("interactive_message")]
public record InteractiveMessagePayload() : InteractionPayload("interactive_message"), IMessagePayload<SlackMessage>
{
    /// <inheritdoc/>
    public override string? ApiAppId
    {
        get => base.ApiAppId ?? OriginalMessage?.AppId;
        init => base.ApiAppId = value;
    }

    /// <summary>
    /// Where it all happened — the user inciting this action clicked a button on a message contained within a channel,
    /// and this property presents the Id and Name of that channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public ChannelInfo Channel { get; init; } = null!;

    /// <summary>
    /// Contains data from the specific interactive component that was used. App surfaces can contain blocks with
    /// multiple interactive components, and each of those components can have multiple values selected by users.
    /// Combine the fields within actions to help provide the full context of the interaction.
    /// </summary>
    [JsonProperty("actions")]
    [JsonPropertyName("actions")]
    public IReadOnlyList<PayloadAction> PayloadActions { get; init; } = Array.Empty<PayloadAction>();

    /// <summary>
    /// The string you provided in the original message attachment as the <c>callback_id</c>. Use this to identify the
    /// specific set of actions/buttons originally posed. If the value of an action is the answer, <c>callback_id</c>
    /// is the specific question that was asked. No more than 200 or so characters please.
    /// </summary>
    [JsonProperty("callback_id")]
    [JsonPropertyName("callback_id")]
    public string CallbackId { get; init; } = null!;

    /// <summary>
    /// The time when the action occurred, expressed in decimal epoch time, wrapped in a string.
    /// Like <c>"1458170917.164398"</c>.
    /// </summary>
    [JsonProperty("action_ts")]
    [JsonPropertyName("action_ts")]
    public string ActionTimestamp { get; init; } = null!;

    /// <summary>
    /// string	The time when the message containing the action was posted, expressed in decimal epoch time,
    /// wrapped in a string. Like <c>"1458170917.164398"</c>.
    /// </summary>
    [JsonProperty("message_ts")]
    [JsonPropertyName("message_ts")]
    public string MessageTimestamp { get; init; } = null!;

    /// <summary>
    /// The thread timestamp.
    /// </summary>
    [JsonProperty("thread_ts")]
    [JsonPropertyName("thread_ts")]
    public string? ThreadTimestamp { get; init; }

    /// <summary>
    /// A 1-indexed identifier for the specific attachment within a message that contained this action.
    /// In case you were curious or building messages containing buttons within many attachments.
    /// </summary>
    [JsonProperty("attachment_id")]
    [JsonPropertyName("attachment_id")]
    public string AttachmentId { get; init; } = null!;

    /// <summary>
    /// Whether or not this message is part of an app unfurl?
    /// </summary>
    /// <remarks>
    /// Undocumented, but seen in the payload.
    /// </remarks>
    [JsonProperty("is_app_unfurl")]
    [JsonPropertyName("is_app_unfurl")]
    public bool IsAppUnfurl { get; init; }

    /// <summary>
    /// The original message that triggered this action. This is especially useful if you don't retain state or
    /// need to know the message's message_ts for use with chat.update This value is not provided for ephemeral
    /// messages.
    /// </summary>
    [JsonProperty("original_message")]
    [JsonPropertyName("original_message")]
    public SlackMessage? OriginalMessage { get; init; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
    SlackMessage? IMessagePayload<SlackMessage>.Message => OriginalMessage;
#pragma warning restore CA1033

    /// <summary>
    /// A short-lived webhook that can be used to
    /// <see href="https://api.slack.com/interactivity/handling#message_responses">send messages</see> in
    /// response to interactions.
    /// </summary>
    [JsonProperty("response_url")]
    [JsonPropertyName("response_url")]
    public Uri ResponseUrl { get; init; } = null!;
}
