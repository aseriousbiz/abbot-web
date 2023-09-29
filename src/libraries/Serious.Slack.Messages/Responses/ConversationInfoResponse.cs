using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The response from the Slack API when retrieving information about a channel.
/// </summary>
public class ConversationInfoResponse : InfoResponse<ConversationInfo>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// Information about the channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public override ConversationInfo? Body { get; init; }
}

/// <summary>
/// The response from the Slack API when retrieving the members of a channel.
/// </summary>
public class ConversationMembersResponse : InfoResponse<IReadOnlyList<string>>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// Information about the channel.
    /// </summary>
    [JsonProperty("members")]
    [JsonPropertyName("members")]
    public override IReadOnlyList<string>? Body { get; init; }
}
