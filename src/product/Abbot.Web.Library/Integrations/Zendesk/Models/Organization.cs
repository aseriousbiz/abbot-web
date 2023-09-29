using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class OrganizationListMessage : ApiMessage<IReadOnlyList<ZendeskOrganization>>
{
    [JsonProperty("organizations")]
    [JsonPropertyName("organizations")]
    public override IReadOnlyList<ZendeskOrganization>? Body { get; set; }
}

public class ZendeskOrganization
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("details")]
    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonProperty("domain_names")]
    [JsonPropertyName("domain_names")]
    public IReadOnlyList<string>? DomainNames { get; set; }

    [JsonProperty("external_id")]
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; set; }

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;
}
