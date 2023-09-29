using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Humanizer;
using Serious.Abbot.Entities;
using Serious.Cryptography;

namespace Serious.Abbot.Integrations.HubSpot;

public class HubSpotSettings : IIntegrationSettings, ITicketingSettings
{
    private const IntegrationType Type = IntegrationType.HubSpot;
#pragma warning disable CA1033
    static IntegrationType IIntegrationSettings.IntegrationType => Type;
    string ITicketingSettings.IntegrationName => Type.Humanize();
    string ITicketingSettings.IntegrationSlug => Type.Humanize(LetterCasing.LowerCase);
    ConversationLinkType ITicketingSettings.ConversationLinkType => ConversationLinkType.HubSpotTicket;
    RoomLinkType ITicketingSettings.RoomLinkType => RoomLinkType.HubSpotCompany;
#pragma warning restore CA1033

    public IntegrationLink? GetTicketLink(ConversationLink? conversationLink) => conversationLink is null
        ? null
        : HubSpotTicketLink.FromConversationLink(conversationLink);

    public IntegrationLink? GetRoomIntegrationLink(RoomLink? roomLink) =>
        HubSpotCompanyLink.Parse(roomLink?.ExternalId);

    /// <summary>
    /// The access token to use for HubSpot requests.
    /// </summary>
    public SecretString? AccessToken { get; set; }

    /// <summary>
    /// The time at which the access token expires, as reported when the token was issued.
    /// NOTE: This is subject to a fairly substantial error margin since it is computed from _our_ server's current UTC time, using the reported
    /// number of seconds until expiry.
    /// </summary>
    public DateTime AccessTokenExpiryUtc { get; set; }

    /// <summary>
    /// The refresh token that can be used to obtain a new access token.
    /// </summary>
    public SecretString? RefreshToken { get; set; }

    /// <summary>
    /// The scopes that were approved for the access/refresh token.
    /// </summary>
    public IList<string>? ApprovedScopes { get; set; }

    /// <summary>
    /// The redirect_uri value that was used when the token was obtained. Required for renewing refresh tokens.
    /// We store this here, rather than regenerating it each time, just in case we change the redirect_uri in the future.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Gets the Hub Domain of the HubSpot Account
    /// </summary>
    public string? HubDomain { get; set; }

    /// <summary>
    /// Gets a boolean indicating if this instance has sufficient credentials to call the HubSpot API.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AccessToken), nameof(RefreshToken), nameof(RedirectUri), nameof(HubDomain))]
    public bool HasApiCredentials => this is { AccessToken: not null, RefreshToken: not null, RedirectUri.Length: > 0, HubDomain.Length: > 0 };

    /// <summary>
    /// The ID of the pipeline to create new tickets in.
    /// </summary>
    public string? TicketPipelineId { get; set; }

    /// <summary>
    /// The ID of the pipeline stage to create <see cref="ConversationState.New"/> tickets in.
    /// </summary>
    public string? NewTicketPipelineStageId { get; set; }

    /// <summary>
    /// The ID of the pipeline stage to move <see cref="ConversationState.Waiting"/> tickets to.
    /// </summary>
    public string? WaitingTicketPipelineStageId { get; set; }

    /// <summary>
    /// The ID of the pipeline stage to move <see cref="ConversationState.NeedsResponse"/> tickets to.
    /// </summary>
    public string? NeedsResponseTicketPipelineStageId { get; set; }

    /// <summary>
    /// The ID of the pipeline stage to move <see cref="ConversationState.Closed"/> tickets to.
    /// </summary>
    public string? ClosedTicketPipelineStageId { get; set; }

    /// <summary>
    /// Gets a boolean indicating if this instance has sufficient configuration to create new tickets.
    /// </summary>
    [MemberNotNullWhen(true, nameof(TicketPipelineId), nameof(NewTicketPipelineStageId))]
    public bool HasTicketConfig => this is { TicketPipelineId: not null, NewTicketPipelineStageId: not null };
}
