using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class UserListMessage : ApiMessage<IReadOnlyList<ZendeskUser>>
{
    [JsonProperty("users")]
    [JsonPropertyName("users")]
    public override IReadOnlyList<ZendeskUser>? Body { get; set; }
}

public class UserMessage : ApiMessage<ZendeskUser>
{
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public override ZendeskUser? Body { get; set; }
}

public class ZendeskUser
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("organization_id")]
    [JsonPropertyName("organization_id")]
    public long? OrganizationId { get; set; }

    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public string Url { get; set; } = null!;

    [JsonProperty("role")]
    [JsonPropertyName("role")]
    public string Role { get; set; } = null!;

    [JsonProperty("external_id")]
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = null!;

    [JsonProperty("email")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonProperty("remote_photo_url")]
    [JsonPropertyName("remote_photo_url")]
    public string? RemotePhotoUrl { get; set; } = null!;

    [JsonProperty("photo")]
    [JsonPropertyName("photo")]
    public Attachment? Photo { get; set; } = null!;

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("notes")]
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonProperty("verified")]
    [JsonPropertyName("verified")]
    public bool Verified { get; set; }
}
