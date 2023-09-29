using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class Requester
{
    [JsonProperty("email")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
}
