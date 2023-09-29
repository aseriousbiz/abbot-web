using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class TicketMessage : ApiMessage<ZendeskTicket>
{
    [JsonProperty("ticket")]
    [JsonPropertyName("ticket")]
    public override ZendeskTicket? Body { get; set; }

    [JsonProperty("audit")]
    [JsonPropertyName("audit")]
    public TicketAudit? Audit { get; set; }
}

public class TicketAudit
{
    [JsonProperty("events")]
    [JsonPropertyName("events")]
    public IReadOnlyList<TicketAuditEvent>? Events { get; set; }
}

public class TicketAuditEvent
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("field_name")]
    [JsonPropertyName("field_name")]
    public string? FieldName { get; set; }

    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [Newtonsoft.Json.JsonExtensionData(ReadData = true, WriteData = true)]
    [System.Text.Json.Serialization.JsonExtensionData]
    public IDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>();
}

public class ZendeskTicket
{
    [JsonProperty("subject")]
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = null!;

    [JsonProperty("external_id")]
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = null!;

    [JsonProperty("comment")]
    [JsonPropertyName("comment")]
    public Comment? Comment { get; set; }

    [JsonProperty("requester")]
    [JsonPropertyName("requester")]
    public Requester? Requester { get; set; }

    [JsonProperty("requester_id")]
    [JsonPropertyName("requester_id")]
    public long? RequesterId { get; set; }

    [JsonProperty("organization_id")]
    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; set; }

    [JsonProperty("submitter_id")]
    [JsonPropertyName("submitter_id")]
    public long? SubmitterId { get; set; }

    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;

    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("priority")]
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonProperty("status")]
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonProperty("tags")]
    [JsonPropertyName("tags")]
    public IList<string> Tags { get; set; } = new List<string>();

    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonProperty("custom_fields")]
    [JsonPropertyName("custom_fields")]
    public IList<CustomFieldValue> CustomFields { get; set; } = new List<CustomFieldValue>();

    [Newtonsoft.Json.JsonExtensionData(ReadData = true, WriteData = true)]
    [System.Text.Json.Serialization.JsonExtensionData]
    public IDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>();
}

public record CustomFieldValue(
    [property: JsonProperty("id")][property: JsonPropertyName("id")] long Id,
    [property: JsonProperty("value")][property: JsonPropertyName("value")] object Value);
