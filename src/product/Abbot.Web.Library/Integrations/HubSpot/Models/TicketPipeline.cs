using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.HubSpot.Models;

public class TicketPipeline : CrmObject
{
    [JsonProperty("displayOrder")]
    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }

    [JsonProperty("label")]
    [JsonPropertyName("label")]
    public string Label { get; set; } = null!;

    [JsonProperty("stages")]
    [JsonPropertyName("stages")]
    public IReadOnlyList<TicketPipelineStage> Stages { get; set; } = Array.Empty<TicketPipelineStage>();
}

public class TicketPipelineStage : CrmObject
{
    [JsonProperty("displayOrder")]
    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }

    [JsonProperty("label")]
    [JsonPropertyName("label")]
    public string Label { get; set; } = null!;

    [JsonProperty("metadata")]
    [JsonPropertyName("metadata")]
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    [JsonProperty("writePermissions")]
    [JsonPropertyName("writePermissions")]
    public string WritePermissions { get; set; } = null!;
}
