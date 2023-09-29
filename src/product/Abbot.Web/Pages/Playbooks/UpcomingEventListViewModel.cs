using System.Collections.Generic;
using Serious.Abbot.Playbooks;

namespace Serious.Abbot.Pages.Playbooks;

public class UpcomingEventListViewModel
{
    public static readonly DomId UpcomingEventsListId = new("upcoming-events-list");
    public required IReadOnlyList<UpcomingPlaybookEvent> Events { get; init; }
}
