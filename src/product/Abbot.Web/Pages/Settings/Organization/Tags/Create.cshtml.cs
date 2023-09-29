using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Filters;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Tags;

[RequirePlanFeature(PlanFeature.ConversationTracking)]
public class CreatePage : UserPage
{
    readonly ITagRepository _tagRepository;

    public CreatePage(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    [Display(Name = "Name")]
    [BindProperty]
    [StringLength(38, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 38 characters in length.")]
    [RegularExpression(Skill.ValidNamePattern, ErrorMessage = Skill.NameErrorMessage)]
    [Remote(action: "Validate", controller: "TagValidation", areaName: "InternalApi")]
    public string NewTagName { get; set; } = null!;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _tagRepository.EnsureTagsAsync(new[] { NewTagName }, null, Viewer, Organization);

        StatusMessage = "Tag created";

        return RedirectToPage("Index");
    }
}
