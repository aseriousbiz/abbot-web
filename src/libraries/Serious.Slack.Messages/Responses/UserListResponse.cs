using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The response from the Slack API when retrieving a list of users.
/// </summary>
public class UserListResponse : InfoResponse<IReadOnlyList<UserInfo>>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The list of retrieved members.
    /// </summary>
    [JsonProperty("members")]
    [JsonPropertyName("members")]
    public override IReadOnlyList<UserInfo>? Body { get; init; }
}

/// <summary>
/// The response from the Slack API when retrieving a list of conversations.
/// </summary>
public class ConversationsResponse : InfoResponse<IReadOnlyList<ConversationInfoItem>>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The list of retrieved channels.
    /// </summary>
    [JsonProperty("channels")]
    [JsonPropertyName("channels")]
    public override IReadOnlyList<ConversationInfoItem>? Body { get; init; }
}
