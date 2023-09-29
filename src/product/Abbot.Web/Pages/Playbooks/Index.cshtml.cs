using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Pages.Playbooks;

[FeatureGate(FeatureFlags.Playbooks)]
public class PlaybooksIndexPage : StaffViewablePage
{
    public DomId PlaybooksListId { get; } = new("playbooks-list");

    readonly PlaybookRepository _repository;
    readonly IClock _clock;

    [BindProperty(Name = "p", SupportsGet = true)]
    public int? PageNumber { get; set; } = 1;

    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Filter { get; set; } // Make this a FilterList later.

    bool FilterApplied => Filter is { Length: > 0 };

    public PlaybooksIndexPage(PlaybookRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public PlaybookListViewModel Playbooks { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        Playbooks = await InitializePageAsync();
    }

    public async Task<IActionResult> OnGetUpcomingAsync()
    {
        // Compute the next 10 events, and compute up to the next 10 occurrences for each scheduled playbook.
        var upcomingEvents = await _repository.GetUpcomingEventsAsync(Organization, 10, 10);

        return Partial("_UpcomingEventsList",
            new UpcomingEventListViewModel()
            {
                Events = upcomingEvents,
            });
    }

    [AllowStaff]
    public async Task<IActionResult> OnPostAsync()
    {
        var playbooks = await InitializePageAsync();
        return TurboUpdate(PlaybooksListId, "_PlaybookList", playbooks);
    }

    async Task<PlaybookListViewModel> InitializePageAsync()
    {
        var pageNumber = Math.Max(1, PageNumber ?? 1);
        var playbooks = await _repository.GetIndexAsync(Organization, Filter, pageNumber, WebConstants.LongPageSize);
        return new PlaybookListViewModel(playbooks, FilterApplied);
    }
}
