using System.Collections.Generic;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.MergeDev.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Integrations.MergeDev;

public class MergeDevLinker : ITicketLinker<TicketingSettings, MergeDevTicket>
{
    readonly IMergeDevClientFactory _clientFactory;
    readonly IConversationRepository _conversationRepository;
    readonly IClock _clock;

    public MergeDevLinker(
        IMergeDevClientFactory clientFactory,
        IConversationRepository conversationRepository,
        IClock clock)
    {
        _clientFactory = clientFactory;
        _conversationRepository = conversationRepository;
        _clock = clock;
    }

    public async Task<MergeDevTicket?> CreateTicketAsync(
        Integration integration,
        TicketingSettings settings,
        IReadOnlyDictionary<string, object?> properties,
        Conversation conversation,
        Member actor)
    {
        var client = _clientFactory.CreateClient(settings);
        var ticket = new Create
        {
            Model = properties,
            IsDebugMode = true,
        };
        var response = await client.CreateTicketAsync(ticket);
        return Expect.NotNull(response.Model);
    }

    public string? GetUserInfo(ApiException apiException)
    {
        // TODO: Deserialize errors
        return apiException.Content;
    }

    public async Task<ConversationLink?> CreateConversationLinkAsync(
        Integration integration,
        TicketingSettings settings,
        MergeDevTicket ticket,
        Conversation conversation,
        Member actor)
    {
        return await _conversationRepository.CreateLinkAsync(
            conversation,
            ConversationLinkType.MergeDevTicket,
            ticket.Id,
            new MergeDevTicketLink.Settings(
                integration,
                settings.IntegrationSlug,
                settings.IntegrationName,
                ticket.TicketUrl),
            actor,
            _clock.UtcNow);
    }
}
