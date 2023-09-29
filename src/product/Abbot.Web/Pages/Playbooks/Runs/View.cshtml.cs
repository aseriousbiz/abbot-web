using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Pages.Playbooks.Runs;

[FeatureGate(FeatureFlags.Playbooks)]
public class ViewModel : StaffViewablePage
{
    readonly PlaybookRepository _playbookRepository;
    readonly PlaybookDispatcher _playbookDispatcher;

    public required PlaybookRun Run { get; set; }

    public ViewModel(PlaybookRepository playbookRepository, PlaybookDispatcher playbookDispatcher)
    {
        _playbookRepository = playbookRepository;
        _playbookDispatcher = playbookDispatcher;
    }

    public async Task<IActionResult> OnGetAsync(string slug, Guid runId)
    {
        // Get the run
        var run = await _playbookRepository.GetRunAsync(runId);
        if (run is null || run.Playbook.Slug != slug)
        {
            return NotFound();
        }

        Run = run;

        return Page();
    }

    [AllowStaff]
    public async Task<IActionResult> OnPostCancelAsync(string slug, string runId, string? staffReason)
    {
        if (InStaffTools && staffReason is null)
        {
            return RedirectWithStatusMessage("Error: Staff reason is required");
        }

        // Get the run
        var run = await _playbookRepository.GetRunAsync(Guid.Parse(runId));
        if (run is null || run.Playbook.Slug != slug)
        {
            return NotFound();
        }

        // Cancel the run
        if (run.State != "Final")
        {
            await _playbookDispatcher.RequestCancellationAsync(run, Viewer, staffReason);
            StatusMessage = "Cancellation requested.";
        }
        else
        {
            StatusMessage = "The playbook has already completed";
        }

        return RedirectToPage();
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Playbooks/Runs/View",
            new {
                id = Organization.PlatformId,
                slug = Run.Playbook.Slug,
                runId = Run.CorrelationId
            });
}
