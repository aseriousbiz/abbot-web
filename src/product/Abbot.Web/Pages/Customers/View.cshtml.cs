using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Collections;

namespace Serious.Abbot.Pages.Customers;

public class ViewPage : StaffViewablePage
{
    readonly CustomerRepository _customerRepository;
    readonly PlaybookRepository _playbookRepository;

    public required Customer Customer { get; set; }
    public required UpcomingEventListViewModel UpcomingPlaybookEvents { get; set; }

    public required IPaginatedList<PlaybookRun> RecentPlaybookRuns { get; set; }

    public ViewPage(CustomerRepository customerRepository, PlaybookRepository playbookRepository)
    {
        _customerRepository = customerRepository;
        _playbookRepository = playbookRepository;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        var customer = await _customerRepository.GetByIdAsync(id, Organization);
        if (customer is null)
        {
            return NotFound();
        }
        Customer = customer;

        // Fetch upcoming playbook events
        UpcomingPlaybookEvents = new()
        {
            // Only compute the next occurrence for any scheduled playbooks
            Events = await _playbookRepository.GetUpcomingEventsAsync(Organization, 10, 1, customer),
        };

        // We're not going to paginate this list, but it could be paginated.
        RecentPlaybookRuns = await _playbookRepository.GetRunsAsync(Organization, customer, 1, WebConstants.ShortPageSize);

        return Page();
    }
}
