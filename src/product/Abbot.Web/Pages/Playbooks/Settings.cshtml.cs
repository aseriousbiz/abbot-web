using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Playbooks;

public class PlaybookSettingsPage : StaffViewablePage
{
    readonly PlaybookRepository _playbookRepository;

    [BindProperty]
    public InputModel Input { get; set; } = null!;
    public Playbook Playbook { get; private set; } = null!;

    public PlaybookSettingsPage(PlaybookRepository playbookRepository)
    {
        _playbookRepository = playbookRepository;
    }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var playbook = await _playbookRepository.GetBySlugAsync(slug, Organization);
        if (playbook is null)
        {
            return NotFound();
        }
        Playbook = playbook;
        Input = new()
        {
            Name = playbook.Name,
            Description = playbook.Description,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var playbook = await _playbookRepository.GetBySlugAsync(slug, Organization);
        if (playbook is null)
        {
            return NotFound();
        }

        await _playbookRepository.UpdatePlaybookAsync(playbook, Input.Name, Input.Description, Viewer);
        return RedirectToPage("View", new { slug });
    }

    public record InputModel
    {
        [Required]
        public required string Name { get; set; }
        public string? Description { get; set; }
    }
}
