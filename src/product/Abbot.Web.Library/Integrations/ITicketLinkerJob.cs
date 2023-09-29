using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations;

public interface ITicketLinkerJob
{
    /// <summary>
    /// Creates a <see cref="ConversationLink"/> between a ticket and a <see cref="Conversation"/>.
    /// </summary>
    /// <param name="organizationId">The Id of the <see cref="Organization"/> that owns this link.</param>
    /// <param name="integrationId">The Id of the ticketing <see cref="Integration"/>.</param>
    /// <param name="conversationId">The Id of the <see cref="Conversation"/> to link.</param>
    /// <param name="messageUrl">The Url to the message where this is linked.</param>
    /// <param name="actorId">The Id of the <see cref="Member"/> who is creating the link.</param>
    /// <param name="actorOrganizationId">The Id of the <see cref="Organization"/> of the actor.</param>
    /// <param name="properties">Additional properties to store with the link.</param>
    /// <returns></returns>
    Task LinkConversationToTicketAsync(
        Id<Organization> organizationId,
        Id<Integration> integrationId,
        Id<Conversation> conversationId,
        Uri? messageUrl,
        Id<Member> actorId,
        Id<Organization> actorOrganizationId,
        IReadOnlyDictionary<string, object?> properties);
}
