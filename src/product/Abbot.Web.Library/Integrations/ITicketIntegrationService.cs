using System.Collections.Generic;
using Hangfire;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations;

public interface ITicketIntegrationService
{
    /// <summary>
    /// Enqueues a request to link a conversation with a ticket.
    /// </summary>
    /// <typeparam name="TSettings">The <see cref="ITicketingSettings"/>.</typeparam>
    /// <param name="integrationId">The <see cref="Integration"/> ID.</param>
    /// <param name="conversation">The <see cref="Conversation"/> to link.</param>
    /// <param name="actor">The <see cref="Member"/> who requested the link. This is NOT the same as the user who should be the requester.</param>
    /// <param name="properties">The properties to populate the ticket.</param>
    void EnqueueTicketLinkRequest<TSettings>(
        Id<Integration> integrationId,
        Conversation conversation,
        Member actor,
        IReadOnlyDictionary<string, object?> properties)
        where TSettings : class, ITicketingSettings;
}

public class TicketIntegrationService : ITicketIntegrationService
{
    readonly IBackgroundJobClient _jobClient;

    public TicketIntegrationService(IBackgroundJobClient jobClient)
    {
        _jobClient = jobClient;
    }

    public void EnqueueTicketLinkRequest<TSettings>(
        Id<Integration> integrationId,
        Conversation conversation,
        Member actor,
        IReadOnlyDictionary<string, object?> properties)
        where TSettings : class, ITicketingSettings
    {
        _jobClient.Enqueue<TicketLinkerJob<TSettings>>(job =>
            job.LinkConversationToTicketAsync(
                conversation.Organization,
                integrationId,
                conversation,
                conversation.GetFirstMessageUrl(),
                actor,
                actor.Organization,
                properties));
    }
}
