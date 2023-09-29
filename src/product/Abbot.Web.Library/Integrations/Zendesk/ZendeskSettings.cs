using System.Diagnostics.CodeAnalysis;
using Humanizer;
using Serious.Abbot.Entities;
using Serious.Abbot.Serialization;
using Serious.Cryptography;

namespace Serious.Abbot.Integrations.Zendesk;

public record ZendeskSettings : JsonSettings, IIntegrationSettings, ITicketingSettings
{
    const IntegrationType Type = IntegrationType.Zendesk;
#pragma warning disable CA1033
    static IntegrationType IIntegrationSettings.IntegrationType => Type;
    string ITicketingSettings.IntegrationName => Type.Humanize();
    string ITicketingSettings.IntegrationSlug => Type.Humanize(LetterCasing.LowerCase);
#pragma warning restore CA1033

    /// <summary>
    /// The Id of the message where we report and update the status of the ticket.
    /// </summary>
    public string? StatusMessageId { get; set; }

#pragma warning disable CA1033
    ConversationLinkType ITicketingSettings.ConversationLinkType => ConversationLinkType.ZendeskTicket;
    RoomLinkType ITicketingSettings.RoomLinkType => RoomLinkType.ZendeskOrganization;
#pragma warning restore CA1033

    public IntegrationLink? GetTicketLink(ConversationLink? conversationLink)
        => ZendeskTicketLink.Parse(conversationLink?.ExternalId);

    public IntegrationLink? GetRoomIntegrationLink(RoomLink? roomLink) =>
        ZendeskOrganizationLink.Parse(roomLink?.ExternalId);

    /// <summary>
    /// Gets or sets the Zendesk subdomain identifying the organization linked by this integration.
    /// </summary>
    public string? Subdomain { get; set; }

    /// <summary>
    /// Gets or sets the API token to use when authenticating with Zendesk.
    /// </summary>
    public SecretString? ApiToken { get; set; }

    /// <summary>
    /// Returns a boolean indicating if there are sufficient settings to connect to Zendesk.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [MemberNotNullWhen(true, nameof(Subdomain), nameof(ApiToken))]
    public bool HasApiCredentials => this is { Subdomain.Length: > 0, ApiToken: not null };

    /// <summary>
    /// The category ID of the Trigger Category created for Abbot-related triggers.
    /// </summary>
    public string? TriggerCategoryId { get; set; }

    /// <summary>
    /// The ID of the Trigger created for notifying Abbot of new comments.
    /// </summary>
    public string? CommentPostedTriggerId { get; set; }

    /// <summary>
    /// The ID of the Webhook created to receive the trigger events.
    /// </summary>
    public string? WebhookId { get; set; }

    /// <summary>
    /// The token that should be sent with every request to the Webhook.
    /// </summary>
    public string? WebhookToken { get; set; }

    /// <summary>
    /// Gets a "token prefix" that's safe for showing to admin users.
    /// </summary>
    /// <returns></returns>
    public string? GetTokenPrefix() => ApiToken?.Reveal().TruncateToLength(8);
}
