using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

/// <summary>
/// Provides metadata about a Zendesk object.
/// </summary>
public class AuditMetadata
{
    /// <summary>
    /// Zendesk-provided metadata about the object.
    /// </summary>
    [JsonProperty("system")]
    [JsonPropertyName("system")]
    public SystemMetadata? System { get; set; }
}

public class SystemMetadata
{
    /// <summary>
    /// The User-Agent of the client that created the object.
    /// </summary>
    [JsonProperty("client")]
    [JsonPropertyName("client")]
    public string? Client { get; set; }
}
