using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// A small set of string attributes about the workspace/team where this action occurred. (Slack calls this a
/// team hash)
/// </summary>
public record TeamIdentifier : EntityBase
{
    /// <summary>
    /// The slack.com subdomain of that same Slack workspace, like <c>watermelonsugar</c>.
    /// </summary>
    [JsonProperty("domain")]
    [JsonPropertyName("domain")]
    public string Domain { get; init; } = null!;
}
