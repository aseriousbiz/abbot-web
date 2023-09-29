using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class OrganizationMembershipMessage : ApiMessage<OrganizationMembership>
{
    [JsonProperty("organization_membership")]
    [JsonPropertyName("organization_membership")]
    public override OrganizationMembership? Body { get; set; }
}

public class OrganizationMembership
{
    [JsonProperty("user_id")]
    [JsonPropertyName("user_id")]
    public long? UserId { get; set; }

    [JsonProperty("organization_id")]
    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; set; }
}
