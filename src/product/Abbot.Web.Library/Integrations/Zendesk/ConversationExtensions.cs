using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations.Zendesk;

public static class ZendeskConversationExtensions
{
    public static ZendeskTicketLink? GetZendeskLink(this Conversation convo)
    {
        if (convo.Links.FirstOrDefault(l => l.LinkType == ConversationLinkType.ZendeskTicket) is not { } link)
        {
            return null;
        }

        return ZendeskTicketLink.Parse(link.ExternalId);
    }
}
