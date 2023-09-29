using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The response from the Slack API when requesting information about a user.
/// </summary>
public class UserInfoResponse : InfoResponse<UserInfo>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The user's information.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public override UserInfo? Body { get; init; }
}

/// <summary>
/// The response from the Slack API when requesting a user's profile information.
/// </summary>
public class UserProfileResponse : InfoResponse<UserProfile>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The user's information.
    /// </summary>
    [JsonProperty("profile")]
    [JsonPropertyName("profile")]
    public override UserProfile? Body { get; init; }
}
