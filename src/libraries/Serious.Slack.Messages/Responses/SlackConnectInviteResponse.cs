using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The response when inviting a user to a slack connect channel.
/// </summary>
public class SlackConnectInviteResponse : ApiResponse
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(InviteId))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The invitation id.
    /// </summary>
    [JsonProperty("invite_id")]
    [JsonPropertyName("invite_id")]
    public string? InviteId { get; init; }

    /// <summary>
    /// Whether or not this is a legacy shared channel
    /// </summary>
    [JsonProperty("is_legacy_shared_channel")]
    [JsonPropertyName("is_legacy_shared_channel")]
    public bool? IsLegacySharedChannel { get; init; }
}
