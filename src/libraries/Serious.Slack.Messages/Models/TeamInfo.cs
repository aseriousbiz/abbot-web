using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Information about a Slack Team. This is returned by the <c>team.info</c> endpoint.
/// </summary>
/// <remarks>
/// https://api.slack.com/methods/team.info
/// </remarks>
public record TeamInfo : TeamIdentifier
{
    /// <summary>
    /// The name of the team.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// The Url for the team or organization that the team belongs to.
    /// </summary>
    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>
    /// The email domain (or domains comma separated) for the team.
    /// </summary>
    [JsonProperty("email_domain")]
    [JsonPropertyName("email_domain")]
    public string? EmailDomain { get; init; }

    /// <summary>
    /// The set of icons for the team.
    /// </summary>
    [JsonProperty("icon")]
    [JsonPropertyName("icon")]
    public Icon Icon { get; init; } = null!;

    /// <summary>
    /// Whether the team is verified or not.
    /// </summary>
    [JsonProperty("is_verified")]
    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; init; }

    /// <summary>
    /// Not sure what this is, saw it in a team.info payload.
    /// </summary>
    [JsonProperty("avatar_base_url")]
    [JsonPropertyName("avatar_base_url")]
    public string? AvatarBaseUrl { get; init; }

    /// <summary>
    /// If part of an Enterprise install, the Enterprise ID for the team.
    /// </summary>
    [JsonProperty("enterprise_id")]
    [JsonPropertyName("enterprise_id")]
    public string? EnterpriseId { get; init; }

    /// <summary>
    /// If part of an Enterprise install, the Enterprise Name for the team.
    /// </summary>
    [JsonProperty("enterprise_name")]
    [JsonPropertyName("enterprise_name")]
    public string? EnterpriseName { get; init; }

    /// <summary>
    /// If part of an Enterprise install, the Enterprise Domain for the team.
    /// </summary>
    [JsonProperty("enterprise_domain")]
    [JsonPropertyName("enterprise_domain")]
    public string? EnterpriseDomain { get; init; }
}
