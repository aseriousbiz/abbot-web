using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Pages.Playbooks;

[FeatureGate(FeatureFlags.Playbooks)]
public class CreatePlaybookPage : UserPage
{
    readonly PlaybookRepository _repository;

    [BindProperty]
    public PlaybookInputModel Input { get; set; } = new();

    public CreatePlaybookPage(PlaybookRepository repository)
    {
        _repository = repository;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _repository.CreateAsync(
            Input.Name,
            Input.Description,
            Input.Name.ToSlug(),
            enabled: true,
            Viewer);

        if (result.Type == EntityResultType.Conflict)
        {
            // This isn't actually a name conflict, it's a slug conflict.
            // But the user doesn't see the slug, so we'll just say it's a name conflict.
            ModelState.AddModelError("Input.Name", "A playbook with a similar name already exists.");
            return Page();
        }

        if (result.IsSuccess)
        {
            StatusMessage = "Playbook created successfully.";
            return RedirectToPage("View",
                new {
                    slug = result.Entity.Slug
                });
        }

        StatusMessage = $"Failed to create playbook: {result.ErrorMessage}.";
        return RedirectToPage();
    }
}

public class PlaybookInputModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; } = null!;
}
