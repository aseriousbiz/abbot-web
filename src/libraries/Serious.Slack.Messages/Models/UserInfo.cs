using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Provides information about a Slack user. We often get these from Slack events such as the user changed event.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/events/user_change"/> for more information.
/// </remarks>
public record UserInfo : UserIdentifier
{
    /// <summary>
    /// Whether or not the user was deleted.
    /// </summary>
    [JsonProperty("deleted")]
    [JsonPropertyName("deleted")]
    public bool Deleted { get; init; }

    /// <summary>
    /// The real name of the user.
    /// </summary>
    [JsonProperty("real_name")]
    [JsonPropertyName("real_name")]
    public string? RealName { get; init; }

    /// <summary>
    /// The user's timezone.
    /// </summary>
    [JsonProperty("tz")]
    [JsonPropertyName("tz")]
    public string? TimeZone { get; init; }

    /// <summary>
    /// Profile information about the user.
    /// </summary>
    [JsonProperty("profile")]
    [JsonPropertyName("profile")]
    public UserProfile Profile { get; init; } = new();

    /// <summary>
    /// Whether the user is a bot or not.
    /// </summary>
    [JsonProperty("is_bot")]
    [JsonPropertyName("is_bot")]
    public bool IsBot { get; init; }

    /// <summary>
    /// Whether the user is a Slack admin or not.
    /// </summary>
    [JsonProperty("is_admin")]
    [JsonPropertyName("is_admin")]
    public bool IsAdmin { get; init; }

    /// <summary>
    /// Whether the user is an owner of the Slack.
    /// </summary>
    [JsonProperty("is_owner")]
    [JsonPropertyName("is_owner")]
    public bool IsOwner { get; init; }

    /// <summary>
    /// Whether the user is the primary owner of the Slack.
    /// </summary>
    [JsonProperty("is_primary_owner")]
    [JsonPropertyName("is_primary_owner")]
    public bool IsPrimaryOwner { get; init; }

    /// <summary>
    /// If present, this is the unix timestamp for when the user was updated.
    /// </summary>
    [JsonProperty("updated")]
    [JsonPropertyName("updated")]
    public long? Updated { get; init; }

    /// <summary>
    /// Indicates whether or not the user is a guest user. Use in combination with the <see cref="IsUltraRestricted"/>
    /// property to check if the user is a single-channel guest user.
    /// </summary>
    [JsonProperty("is_restricted")]
    [JsonPropertyName("is_restricted")]
    public bool IsRestricted { get; init; }

    /// <summary>
    /// Indicates whether or not the user is a single-channel guest.
    /// </summary>
    [JsonProperty("is_ultra_restricted")]
    [JsonPropertyName("is_ultra_restricted")]
    public bool IsUltraRestricted { get; init; }
}
