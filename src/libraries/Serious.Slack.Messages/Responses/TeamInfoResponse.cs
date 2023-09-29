using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Response from the <c>team.info</c> endpoint
/// </summary>
public class TeamInfoResponse : InfoResponse<TeamInfo>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// Information about a Slack Team. This is returned by the <c>team.info</c> endpoint.
    /// </summary>
    [JsonProperty("team")]
    [JsonPropertyName("team")]
    public override TeamInfo? Body { get; init; }
}
