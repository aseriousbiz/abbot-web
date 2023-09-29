using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Pages.Playbooks;

[FeatureGate(FeatureFlags.Playbooks)]
public class DeletePlaybookPage : UserPage
{
    readonly PlaybookRepository _repository;

    public DeletePlaybookPage(PlaybookRepository repository)
    {
        _repository = repository;
    }

    public Playbook Playbook { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var result = await InitializePageAsync(slug);
        if (result is not null)
        {
            return result;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug)
    {
        var result = await InitializePageAsync(slug);
        if (result is not null)
        {
            return result;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var playbook = await _repository.DeleteAsync(Playbook, Viewer);
        StatusMessage = "Playbook deleted successfully.";
        return RedirectToPage("Index", null, fragment: playbook.Slug);
    }

    async Task<IActionResult?> InitializePageAsync(string slug)
    {
        var playbook = await _repository.GetBySlugAsync(slug, Organization);
        if (playbook is null)
        {
            return NotFound();
        }
        Playbook = playbook;

        return null;
    }
}
