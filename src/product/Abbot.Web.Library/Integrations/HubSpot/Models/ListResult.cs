using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.HubSpot.Models;

public class ListResult<T>
{
    [JsonProperty("results")]
    [JsonPropertyName("results")]
    public IReadOnlyList<T> Results { get; set; } = Array.Empty<T>();
}
