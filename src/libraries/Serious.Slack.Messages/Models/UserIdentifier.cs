using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Information about a user.
/// </summary>
public record UserIdentifier : EntityBase
{
    /// <summary>
    /// The name of that very same user (This would be the profile display_name).
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The user's Username
    /// </summary>
    [JsonProperty("username")]
    [JsonPropertyName("username")]
    public string? UserName { get; init; }

    /// <summary>
    /// The team that the user belongs to.
    /// This can be <c>null</c>, which usually indicates that the user is in the same team that this Abbot instance is in.
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string? TeamId { get; init; }

    /// <summary>
    /// The unique identifier for the enterprise the user belongs to.
    /// </summary>
    [JsonProperty("enterprise_id")]
    [JsonPropertyName("enterprise_id")]
    public string? EnterpriseId { get; init; }

    /// <summary>
    /// If the user is an enterprise user, this contains more information about the user.
    /// </summary>
    [JsonProperty("enterprise_user")]
    [JsonPropertyName("enterprise_user")]
    public EnterpriseUser? EnterpriseUser { get; init; }
}
