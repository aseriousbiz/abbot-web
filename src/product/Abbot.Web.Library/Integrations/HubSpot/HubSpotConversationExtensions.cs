using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations.HubSpot;

public static class HubSpotConversationExtensions
{
    public static HubSpotTicketLink? GetHubSpotLink(this Conversation convo)
    {
        return convo.Links.FirstOrDefault(l => l.LinkType is ConversationLinkType.HubSpotTicket) is not { } link
            ? null
            : HubSpotTicketLink.FromConversationLink(link);
    }
}

