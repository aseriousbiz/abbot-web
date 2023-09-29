using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Pages.Playbooks.Versions;

[FeatureGate(FeatureFlags.Playbooks)]
public class IndexModel : StaffViewablePage
{
    readonly PlaybookRepository _playbookRepository;

    public required VersionListModel VersionList { get; set; }

    public IndexModel(PlaybookRepository playbookRepository)
    {
        _playbookRepository = playbookRepository;
    }

    public async Task<IActionResult> OnGetAsync(string slug, int? p = null)
    {
        // Get the playbook
        if (await _playbookRepository.GetBySlugAsync(slug, Organization) is not { } playbook)
        {
            return NotFound();
        }

        // Get the latest published version
        var latest =
            await _playbookRepository.GetCurrentVersionAsync(playbook, includeDraft: true, includeDisabled: true);

        var latestPublished =
            await _playbookRepository.GetCurrentVersionAsync(playbook, includeDraft: false, includeDisabled: true);

        // Get all the versions
        var allVersions = await _playbookRepository.GetAllVersionsAsync(playbook, p ?? 1, WebConstants.LongPageSize);

        VersionList = new VersionListModel(playbook, latest, latestPublished, allVersions);
        return Page();
    }
}
