using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.HubSpot.Models;

public class TimelineEvent
{
    [JsonProperty("eventTemplateId")]
    [JsonPropertyName("eventTemplateId")]
    public string EventTemplateId { get; set; } = null!;

    [JsonProperty("objectId")]
    [JsonPropertyName("objectId")]
    public string ObjectId { get; set; } = null!;

    [JsonProperty("tokens")]
    [JsonPropertyName("tokens")]
    public IDictionary<string, object?> Tokens { get; set; } = null!;

    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
}
