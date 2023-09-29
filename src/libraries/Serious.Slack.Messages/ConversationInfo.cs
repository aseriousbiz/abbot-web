using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Information about a Slack conversation (channel). <see href="https://api.slack.com/types/conversation"/>. This
/// has additional information about a conversation such as when called from <c>conversation.info</c>.
/// </summary>
[DebuggerDisplay("{Name} ({Id})")]
public record ConversationInfo : ConversationInfoItem
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    public ConversationInfo()
    {
    }

    /// <summary>
    /// Creates a <see cref="ConversationInfo"/> from a <see cref="ConversationInfoItem"/>.
    /// </summary>
    /// <param name="conversationInfoItem">The <see cref="ConversationInfoItem"/></param>
    [SetsRequiredMembers]
    public ConversationInfo(ConversationInfoItem conversationInfoItem) : base(conversationInfoItem)
    {
        Id = conversationInfoItem.Id;
    }

    /// <summary>
    /// If <c>true</c>, the user who called the "conversation.info" API is a member of this conversation.
    /// </summary>
    [JsonProperty("is_member")]
    [JsonPropertyName("is_member")]
    public bool IsMember { get; init; }

    /// <summary>
    /// The number of members in the conversation, if <c>include_num_members</c> is specified.
    /// </summary>
    [JsonProperty("num_members")]
    [JsonPropertyName("num_members")]
    public int? MemberCount { get; init; }
}

/// <summary>
/// Information about a Slack conversation (channel). <see href="https://api.slack.com/types/conversation"/> when
/// returned as part of a list of conversations. For example, from the <c>users.conversations</c> API.
/// </summary>
[DebuggerDisplay("{Name} ({Id})")]
public record ConversationInfoItem : EntityBase
{
    /// <summary>
    /// The room name.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The normalized name of the room.
    /// </summary>
    [JsonProperty("name_normalized")]
    [JsonPropertyName("name_normalized")]
    public string NameNormalized { get; init; } = null!;

    /// <summary>
    /// If <c>true</c>, this is a channel (as opposed to a DM or group DM, etc.).
    /// </summary>
    [JsonProperty("is_channel")]
    [JsonPropertyName("is_channel")]
    public bool IsChannel { get; init; }

    /// <summary>
    /// If <c>true</c>, this is a private conversation.
    /// </summary>
    [JsonProperty("is_private")]
    [JsonPropertyName("is_private")]
    public bool IsPrivate { get; init; }

    /// <summary>
    /// If <c>true</c>, this is a group conversation.
    /// </summary>
    [JsonProperty("is_group")]
    [JsonPropertyName("is_group")]
    public bool IsGroup { get; init; }

    /// <summary>
    /// If <c>true</c>, this is a 1-1 instant message.
    /// </summary>
    [JsonProperty("is_im")]
    [JsonPropertyName("is_im")]
    public bool IsInstantMessage { get; init; }

    /// <summary>
    /// If <c>true</c>, this is a multi-party instant message.
    /// </summary>
    [JsonProperty("is_mpim")]
    [JsonPropertyName("is_mpim")]
    public bool IsMultipartyInstantMessage { get; init; }

    /// <summary>
    /// If <c>true</c>, this conversation is archived.
    /// </summary>
    [JsonProperty("is_archived")]
    [JsonPropertyName("is_archived")]
    public bool IsArchived { get; init; }

    /// <summary>
    /// If <c>true</c>, this conversation is in the #general channel.
    /// </summary>
    [JsonProperty("is_general")]
    [JsonPropertyName("is_general")]
    public bool IsGeneral { get; init; }

    /// <summary>
    /// If this is a direct message, then this is the user ID of the other person in the conversation.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public string? User { get; init; }

    /// <summary>
    /// Indicates whether a conversation is in some way shared between multiple workspaces. This is <code>true</code>
    /// if <see cref="IsExternallyShared"/> or <see cref="IsOrganizationShared"/> is <code>true</code>.
    /// </summary>
    [JsonProperty("is_shared")]
    [JsonPropertyName("is_shared")]
    public bool IsShared { get; init; }

    /// <summary>
    /// Indicates whether a conversation is part of a Shared Channel with a remote organization. If <c>true</c>, then
    /// <see cref="IsShared"/> is also <code>true</code>.
    /// </summary>
    [JsonProperty("is_ext_shared")]
    [JsonPropertyName("is_ext_shared")]
    public bool IsExternallyShared { get; init; }

    /// <summary>
    /// Indicates the conversation is awaiting approval to become an externally shared channel.
    /// </summary>
    [JsonProperty("is_pending_ext_shared")]
    [JsonPropertyName("is_pending_ext_shared")]
    public bool IsPendingExternallyShared { get; init; }

    /// <summary>
    /// Indicates whether a conversation is part of a Shared Channel with another Enterprise Grid workspace in the
    /// same organization. If <c>true</c>, then <see cref="IsShared"/> is also <code>true</code>.
    /// </summary>
    [JsonProperty("is_org_shared")]
    [JsonPropertyName("is_org_shared")]
    public bool IsOrganizationShared { get; init; }

    /// <summary>
    /// A timestamp of when the conversation was created.
    /// </summary>
    [JsonProperty("created")]
    [JsonPropertyName("created")]
    public long Created { get; init; }

    /// <summary>
    /// The date the conversation was created, calculated from the <see cref="Created"/> timestamp.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTimeOffset CreatedDate => DateTimeOffset.FromUnixTimeSeconds(Created);

    /// <summary>
    /// Slack Id of the user that created this conversation.
    /// </summary>
    [JsonProperty("creator")]
    [JsonPropertyName("creator")]
    public string Creator { get; init; } = null!;

    /// <summary>
    /// The topic of the conversation.
    /// </summary>
    [JsonProperty("topic")]
    [JsonPropertyName("topic")]
    public TopicInfo? Topic { get; init; }

    /// <summary>
    /// The purpose of the conversation.
    /// </summary>
    [JsonProperty("purpose")]
    [JsonPropertyName("purpose")]
    public TopicInfo? Purpose { get; init; }

    /// <summary>
    /// The previous names of the conversation.
    /// </summary>
    [JsonProperty("previous_names")]
    [JsonPropertyName("previous_names")]
    public IReadOnlyList<string> PreviousNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The locale for this conversation, if <c>include_locale</c> is specified.
    /// </summary>
    [JsonProperty("locale")]
    [JsonPropertyName("locale")]
    public string? Locale { get; init; }
}
