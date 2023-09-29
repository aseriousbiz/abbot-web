using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// The original message that included interactive elements that triggered this payload.
/// This is especially useful if you don't retain state or need to know the message's message_ts
/// for use with chat.update This value is not provided for ephemeral
/// messages.
/// </summary>
public record SlackMessage(string? Text) : MessageBase(Text)
{
    /// <summary>
    /// Constructs a <see cref="SlackMessage"/>
    /// </summary>
    public SlackMessage() : this((string?)null)
    {
    }

    /// <summary>
    /// The type, which should be <c>message</c> or <c>app_mention</c>.
    /// </summary>
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; init; } = "message";

    /// <summary>
    /// The sub type of message. This is typically <c>bot_message</c>.
    /// </summary>
    [JsonProperty("subtype")]
    [JsonPropertyName("subtype")]
    public string SubType { get; init; } = null!;

    /// <summary>
    /// The Id of the user that sent this message.
    /// </summary>
    /// <remarks>
    /// This is usually part of the event body. However, when passing along the source message, this is provided here.
    /// </remarks>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public string? User { get; init; }

    /// <summary>
    /// The unique (per-channel) timestamp for the message.
    /// </summary>
    /// <remarks>Is <c>null</c> in rare cases, such as when returned from a <c>chat.update</c> call.</remarks>
    [JsonProperty("ts")]
    [JsonPropertyName("ts")]
    public string? Timestamp { get; set; }

    /// <summary>
    /// Provide another message's ts value to make this message a reply.
    /// Avoid using a reply's ts value; use its parent instead.
    /// </summary>
    [JsonProperty("thread_ts")]
    [JsonPropertyName("thread_ts")]
    public string? ThreadTimestamp { get; init; }

    /// <summary>
    /// The icons for the sender.
    /// </summary>
    [JsonProperty("icons")]
    [JsonPropertyName("icons")]
    public BotIcons? Icons { get; init; }

    /// <summary>
    /// The App Id. Not to be confused with the Bot Id nor the Bot's user id. Ex. A0123456789.
    /// </summary>
    [JsonProperty("app_id")]
    [JsonPropertyName("app_id")]
    public string AppId { get; init; } = null!;

    /// <summary>
    /// The set of attachments in the original message. This would include the set of interactive elements.
    /// </summary>
    [JsonProperty("attachments")]
    [JsonPropertyName("attachments")]
    public IReadOnlyList<LegacyMessageAttachment> Attachments { get; init; } = Array.Empty<LegacyMessageAttachment>();

    /// <summary>
    /// The Id of the team.
    /// </summary>
    [JsonProperty("team")]
    [JsonPropertyName("team")]
    public string TeamId { get; init; } = null!;

    /// <summary>
    /// The Id of the source team when posted by a user from another workspace in a shared channel.
    /// </summary>
    [JsonProperty("source_team")]
    [JsonPropertyName("source_team")]
    public string? SourceTeam { get; init; }

    /// <summary>
    /// The set of blocks in the original message.
    /// </summary>
    [JsonProperty("blocks")]
    [JsonPropertyName("blocks")]
    public IReadOnlyList<ILayoutBlock> Blocks { get; init; } = Array.Empty<ILayoutBlock>();

    /// <summary>
    /// The set of file attachments in the message.
    /// </summary>
    [JsonProperty("files")]
    [JsonPropertyName("files")]
    public IReadOnlyList<FileUpload> Files { get; init; } = Array.Empty<FileUpload>();

    /// <summary>
    /// Profile information about the sender of this message. This is included in cases when <c>include_all_metadata</c>
    /// is <c>true</c> when calling <c>conversations.history</c>.
    /// </summary>
    [JsonProperty("user_profile")]
    [JsonPropertyName("user_profile")]
    public UserProfileMetadata? UserProfile { get; init; }

    /// <summary>
    /// Returns the number of replies to this message.
    /// </summary>
    /// <remarks>
    /// This is usually present in the first message when calling conversation.replies.
    /// </remarks>
    [JsonProperty("reply_count")]
    [JsonPropertyName("reply_count")]
    public int? ReplyCount { get; set; }

    /// <summary>
    /// The IDs of the users that replied to this message.
    /// </summary>
    /// <remarks>
    /// This is usually present in the first message when calling conversation.replies.
    /// </remarks>
    [JsonProperty("reply_users")]
    [JsonPropertyName("reply_users")]
    public IReadOnlyList<string> ReplyUsers { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Returns the number of users that replied to this message.
    /// </summary>
    /// <remarks>
    /// This is usually present in the first message when calling conversation.replies.
    /// </remarks>    [JsonProperty("reply_users_count")]
    [JsonPropertyName("reply_users_count")]
    public int? ReplyUsersCount { get; set; }

    /// <summary>
    /// If present, specifies the timestamp of the latest reply in the thread.
    /// </summary>
    /// <remarks>
    /// This is usually present in the first message when calling conversation.replies.
    /// </remarks>
    [JsonProperty("latest_reply")]
    [JsonPropertyName("latest_reply")]
    public string? LatestReplyTimestamp { get; init; }

    /// <summary>
    /// If present, contains information about the Bot that posted this message.
    /// </summary>
    [JsonProperty("bot_profile")]
    [JsonPropertyName("bot_profile")]
    public BotProfile? BotProfile { get; init; }
}
