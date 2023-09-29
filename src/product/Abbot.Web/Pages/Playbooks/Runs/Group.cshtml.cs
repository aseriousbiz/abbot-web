using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Pages.Playbooks.Runs;

[FeatureGate(FeatureFlags.Playbooks)]
public class GroupsModel : StaffViewablePage
{
    readonly PlaybookRepository _playbookRepository;
    readonly PlaybookDispatcher _playbookDispatcher;

    public required PlaybookRunGroup Group { get; set; }

    public bool CanBeCancelled { get; set; }

    public GroupsModel(PlaybookRepository playbookRepository, PlaybookDispatcher playbookDispatcher)
    {
        _playbookRepository = playbookRepository;
        _playbookDispatcher = playbookDispatcher;
    }

    public async Task<IActionResult> OnGetAsync(string slug, Guid groupId)
    {
        // Get the run
        var group = await _playbookRepository.GetRunGroupAsync(groupId);
        if (group is null || group.Playbook.Slug != slug)
        {
            return NotFound();
        }

        Group = group;

        if (Group.Runs.Count == 1)
        {
            // Just go straight to the run
            return RedirectToPage("View", new { slug, groupId, runId = Group.Runs.First().CorrelationId });
        }

        CanBeCancelled = Group.Runs.Any(r => r.State != "Final");

        return Page();
    }

    [AllowStaff]
    public async Task<IActionResult> OnPostCancelAsync(string slug, Guid groupId, string? staffReason)
    {
        if (InStaffTools && staffReason is null)
        {
            return RedirectWithStatusMessage("Error: Staff reason is required");
        }

        // Get the run
        var group = await _playbookRepository.GetRunGroupAsync(groupId);
        if (group is null || group.Playbook.Slug != slug)
        {
            return NotFound();
        }

        // Cancel all runs
        foreach (var run in group.Runs.Where(r => r.State != "Final"))
        {
            await _playbookDispatcher.RequestCancellationAsync(run, Viewer, staffReason);
        }

        StatusMessage = "Cancellation requested.";

        return RedirectToPage();
    }
}
