using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.HubSpot.Models;

public class CrmObjectId
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
}

public class CrmObject : CrmObjectId
{
    [JsonProperty("createdAt")]
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updatedAt")]
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("archived")]
    [JsonPropertyName("archived")]
    public bool Archived { get; set; }
}

public class HubSpotContact : CrmObject
{
    // TODO: Move up into CrmObject
    [JsonProperty("properties")]
    [JsonPropertyName("properties")]
    public IReadOnlyDictionary<string, string?> Properties { get; set; } = null!;
}
