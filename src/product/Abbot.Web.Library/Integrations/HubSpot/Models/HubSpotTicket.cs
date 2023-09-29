using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.HubSpot.Models;

public class HubSpotTicket : CrmObject
{
    [JsonProperty("properties")]
    [JsonPropertyName("properties")]
    public IDictionary<string, string?> Properties { get; set; } = null!;
}

public class CreateOrUpdateTicket
{
    [JsonProperty("properties")]
    [JsonPropertyName("properties")]
    public IDictionary<string, string?> Properties { get; set; } = null!;

    [JsonProperty("associations")]
    [JsonPropertyName("associations")]
    public IList<CreateHubSpotAssociation>? Associations { get; set; }
}

public class TicketProperties
{
    [JsonProperty("hs_pipeline")]
    [JsonPropertyName("hs_pipeline")]
    public string Pipeline { get; set; } = null!;

    [JsonProperty("hs_pipeline_stage")]
    [JsonPropertyName("hs_pipeline_stage")]
    public string Stage { get; set; } = null!;

    [JsonProperty("hs_ticket_priority")]
    [JsonPropertyName("hs_ticket_priority")]
    public string Priority { get; set; } = null!;

    [JsonProperty("subject")]
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = null!;

    [JsonProperty("content")]
    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}
