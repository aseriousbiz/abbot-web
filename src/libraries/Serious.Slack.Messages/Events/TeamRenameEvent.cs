using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Event raised when a team is renamed. See <see href="https://api.slack.com/events/team_rename"/>.
/// </summary>
[Element("team_rename")]
public sealed record TeamRenameEvent() : EventBody("team_rename")
{
    /// <summary>
    /// The new team name.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// The Id of the team.
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string TeamId { get; init; } = null!;
}

/// <summary>
/// Event raised when a team's domain is changed. See <see href="https://api.slack.com/events/team_domain_change"/>.
/// </summary>
[Element("team_domain_change")]
public sealed record TeamDomainChangeEvent() : EventBody("team_domain_change")
{
    /// <summary>
    /// The URL to the Slack workspace.
    /// </summary>
    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public Uri Url { get; init; } = null!;

    /// <summary>
    /// The new domain.
    /// </summary>
    [JsonProperty("domain")]
    [JsonPropertyName("domain")]
    public string Domain { get; init; } = null!;

    /// <summary>
    /// The Id of the team.
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string TeamId { get; init; } = null!;
}
