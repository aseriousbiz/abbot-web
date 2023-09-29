using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Conversations;

/// <summary>
/// Renders information about a pending ticket being created and linked to a <see cref="Conversation"/>. We could
/// support this asynchronous model for multiple integrations, but for now we only support HubSpot.
/// </summary>
public class PendingPageModel : UserPage
{
    readonly IHubSpotLinker _hubSpotTicketLinker;
    readonly IConversationRepository _conversationRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly IMessageRenderer _messageRenderer;
    readonly IHubSpotClientFactory _clientFactory;
    readonly IDataProtectionProvider _dataProtectionProvider;
    readonly ISettingsManager _settingsManager;

    public Conversation Conversation { get; private set; } = null!;

    public RenderedMessage ConversationTitle { get; private set; } = null!;

    public IntegrationType IntegrationType { get; private set; }

    public string? SearchResults { get; private set; }

    public PendingPageModel(
        IHubSpotLinker hubSpotTicketLinker,
        IConversationRepository conversationRepository,
        IIntegrationRepository integrationRepository,
        IMessageRenderer messageRenderer,
        IHubSpotClientFactory clientFactory,
        ISettingsManager settingsManager,
        IDataProtectionProvider dataProtectionProvider)
    {
        _hubSpotTicketLinker = hubSpotTicketLinker;
        _conversationRepository = conversationRepository;
        _integrationRepository = integrationRepository;
        _messageRenderer = messageRenderer;
        _clientFactory = clientFactory;
        _dataProtectionProvider = dataProtectionProvider;
        _settingsManager = settingsManager;
    }

    public async Task<IActionResult> OnGetAsync(int conversationId, IntegrationType integrationType, int actorId)
    {
        var conversation = await _conversationRepository.GetConversationAsync(conversationId);
        if (conversation is null)
        {
            return NotFound();
        }

        var link = await _hubSpotTicketLinker.LinkPendingConversationTicketAsync(
            conversation,
            Viewer);

        if (link?.ExternalId is { } ticketUrl)
        {
            return Redirect(ticketUrl);
        }

        Conversation = conversation;

        ConversationTitle = await _messageRenderer.RenderMessageAsync(
            conversation.Title,
            Organization);

        return Page();
    }
}
