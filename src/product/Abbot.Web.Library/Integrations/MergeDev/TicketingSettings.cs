using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Cryptography;

namespace Serious.Abbot.Integrations.MergeDev;

public class TicketingSettings : IIntegrationSettings, ITicketingSettings
{
    const IntegrationType Type = IntegrationType.Ticketing;
#pragma warning disable CA1033
    static IntegrationType IIntegrationSettings.IntegrationType => Type;
    ConversationLinkType ITicketingSettings.ConversationLinkType => ConversationLinkType.MergeDevTicket;

    ConversationLink? ITicketingSettings.FindLink(Conversation? conversation, Integration integration) =>
#pragma warning restore CA1033
        conversation?.Links.SingleOrDefault(l =>
            l.LinkType == ConversationLinkType.MergeDevTicket
            // TODO: Add ConversationLink.IntegrationId?
            && MergeDevTicketLink.From(l) is { } ticketLink
            && ticketLink.IntegrationId == integration
        );

    public IntegrationLink? GetTicketLink(ConversationLink? conversationLink) =>
        MergeDevTicketLink.From(conversationLink);

    public string IntegrationName =>
        AccountDetails?.Integration ?? "Unknown Service";

    public string IntegrationSlug =>
        AccountDetails?.IntegrationSlug ?? "unknown";

    public string AnalyticsSlug => $"merge:{IntegrationSlug}";

    /// <summary>
    /// Gets a boolean indicating if this instance has sufficient credentials to call the Merge API.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AccessToken))]
    public bool HasApiCredentials => this is { AccessToken: not null };

    // TODO: Rename AccountToken
    public SecretString? AccessToken { get; set; }

    public TicketingAccountDetails? AccountDetails { get; set; }
}

public record TicketingAccountDetails
{
    [Display(Name = "Account ID")]
    [Required]
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [Display(Name = "Integration")]
    [Required]
    [JsonProperty("integration")]
    [JsonPropertyName("integration")]
    public string? Integration { get; set; }

    [Display(Name = "Integration Slug")]
    [Required]
    [JsonProperty("integration_slug")]
    [JsonPropertyName("integration_slug")]
    public string? IntegrationSlug { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [MemberNotNullWhen(true, nameof(Id), nameof(Integration), nameof(IntegrationSlug))]
    public bool IsValid =>
        this is
        {
            Id.Length: > 0,
            Integration.Length: > 0,
            IntegrationSlug.Length: > 0,
        };
}
