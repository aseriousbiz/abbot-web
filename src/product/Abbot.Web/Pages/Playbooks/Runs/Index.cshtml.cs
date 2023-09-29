using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;
using Serious.Collections;

namespace Serious.Abbot.Pages.Playbooks.Runs;

[FeatureGate(FeatureFlags.Playbooks)]
public class IndexModel : StaffViewablePage
{
    readonly PlaybookRepository _playbookRepository;
    readonly StepTypeCatalog _stepTypeCatalog;

    public required Playbook Playbook { get; set; }

    public required IPaginatedList<RunGroupViewModel> RunGroups { get; set; }

    public IndexModel(PlaybookRepository playbookRepository, StepTypeCatalog stepTypeCatalog)
    {
        _playbookRepository = playbookRepository;
        _stepTypeCatalog = stepTypeCatalog;
    }

    public async Task<IActionResult> OnGetAsync(string slug, int? p = null)
    {
        // Get the playbook
        if (await _playbookRepository.GetBySlugAsync(slug, Organization) is not { } playbook)
        {
            return NotFound();
        }

        Playbook = playbook;

        // Get all runs for the playbook
        var groups = await _playbookRepository.GetRunGroupsAsync(Organization, slug, p ?? 1, WebConstants.LongPageSize);
        RunGroups = groups.Map(summary => new RunGroupViewModel()
        {
            Summary = summary,
            TriggerType = summary.Group.Properties.TriggerType is { } triggerType
                ? _stepTypeCatalog.TryGetType(triggerType, out var t)
                    ? t
                    : null
                : null,
        });
        return Page();
    }
}

public record RunGroupViewModel
{
    public PlaybookRunGroup Group => Summary.Group;
    public required PlaybookRunGroupSummary Summary { get; init; }
    public required StepType? TriggerType { get; init; }
}
