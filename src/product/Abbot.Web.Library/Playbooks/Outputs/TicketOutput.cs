using System.Text.Json.Serialization;

namespace Serious.Abbot.Playbooks.Outputs;

public record TicketOutput
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("url")]
    public required string? Url { get; init; }

    [JsonPropertyName("api_url")]
    public required string ApiUrl { get; init; }

    [JsonPropertyName("ticket_id")]
    public required string? TicketId { get; init; }

    // TODO: Add normalized status for display but not filtering?
    // [JsonPropertyName("status")]
    // public string? Status { get; init; }

    [JsonPropertyName("github")]
    public GitHubOutput? GitHub { get; init; }

    [JsonPropertyName("hubspot")]
    public HubSpotOutput? HubSpot { get; init; }

    [JsonPropertyName("zendesk")]
    public ZendeskOutput? Zendesk { get; init; }

    public record GitHubOutput(
        [property: JsonPropertyName("owner")]
        string Owner,
        [property: JsonPropertyName("repo")]
        string Repo);

    public record HubSpotOutput(
        [property: JsonPropertyName("thread_id")]
        string? ThreadId);

    public record ZendeskOutput
    {
        /// <summary>
        /// Zendesk Status: New, Open, Pending, Solved, Closed
        /// </summary>
        [JsonPropertyName("status")]
        public required string Status { get; init; }
    }
}
