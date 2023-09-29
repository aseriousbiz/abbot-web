using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Information about an enterprise user.
/// </summary>
public record EnterpriseUser : EntityBase
{
    /// <summary>
    /// The Id of the enterprise.
    /// </summary>
    [JsonProperty("enterprise_id")]
    [JsonPropertyName("enterprise_id")]
    public string EnterpriseId { get; init; } = null!;

    /// <summary>
    /// The name of the enterprise.
    /// </summary>
    [JsonProperty("enterprise_name")]
    [JsonPropertyName("enterprise_name")]
    public string EnterpriseName { get; init; } = null!;

    /// <summary>
    /// If <c>true</c>, the user is an administrator of the enterprise.
    /// </summary>
    [JsonProperty("is_admin")]
    [JsonPropertyName("is_admin")]
    public bool IsAdmin { get; init; }

    /// <summary>
    /// If <c>true</c>, the user is an owner of the enterprise.
    /// </summary>
    [JsonProperty("is_owner")]
    [JsonPropertyName("is_owner")]
    public bool IsOwner { get; init; }

    /// <summary>
    /// The set of teams that the user is a member of.
    /// </summary>
    [JsonProperty("teams")]
    [JsonPropertyName("teams")]
    public IReadOnlyList<string> Teams { get; init; } = Array.Empty<string>();
}
