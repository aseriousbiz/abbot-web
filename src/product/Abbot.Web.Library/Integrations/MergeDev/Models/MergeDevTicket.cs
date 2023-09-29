using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.MergeDev.Models;

/// <summary>
/// A Merge.dev common ticket.
/// </summary>
public record MergeDevTicket
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonProperty("remote_id")]
    [JsonPropertyName("remote_id")]
    public string? RemoteId { get; init; }

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonProperty("description")]
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonProperty("priority")]
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }

    [JsonProperty("ticket_url")]
    [JsonPropertyName("ticket_url")]
    public string? TicketUrl { get; init; }
}
