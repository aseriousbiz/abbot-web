using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// An installation of the Slack App.
/// </summary>
/// <remarks>
/// Installations are defined by a combination of the installing Enterprise Grid org, workspace, and user
/// (represented by <c>enterprise_id</c>, <c>team_id</c>, and <c>user_id</c> inside this field) —
/// note that installations may only have one or two, not all three, defined. authorizations describes one of the
/// installations that this event is visible to. You'll receive a single event for a piece of data intended for
/// multiple users in a workspace, rather than a message per user. Use apps.event.authorizations.list to retrieve
/// additional authorizations.
/// </remarks>
public class Authorization
{
    /// <summary>
    /// The unique identifier for the user that is authorized.
    /// </summary>
    [JsonProperty("user_id")]
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    /// <summary>
    /// The unique identifier for the workspace/team.
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string? TeamId { get; set; }

    /// <summary>
    /// Whether this authorization is a bot.
    /// </summary>
    [JsonProperty("is_bot")]
    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    /// <summary>
    /// Whether this authorization is for an enterprise install.
    /// </summary>
    [JsonProperty("is_enterprise_install")]
    [JsonPropertyName("is_enterprise_install")]
    public bool IsEnterpriseInstall { get; set; }

    /// <summary>
    /// The unique identifier for the enterprise.
    /// </summary>
    [JsonProperty("enterprise_id")]
    [JsonPropertyName("enterprise_id")]
    public string? EnterpriseId { get; set; }
}
