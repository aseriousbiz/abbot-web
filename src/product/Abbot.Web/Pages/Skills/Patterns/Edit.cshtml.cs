using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Patterns;

public class EditPageModel : SkillFeatureEditPageModel
{
    readonly IPatternRepository _patternRepository;
    readonly IPermissionRepository _permissions;
    SkillPattern _skillPattern = null!;

    public EditPageModel(
        IPatternRepository patternRepository,
        IPermissionRepository permissions)
    {
        _patternRepository = patternRepository;
        _permissions = permissions;
    }

    [BindProperty]
    public PatternInputModel Input { get; set; } = new();

    public PatternTestInputModel TestInput { get; set; } = new();

    public string Slug { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        string skill,
        string slug)
    {
        var user = await InitializePageState(skill, slug);
        if (user is null)
        {
            return NotFound();
        }
        Input.Initialize(_skillPattern);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string skill,
        string slug)
    {
        var user = await InitializePageState(skill, slug);
        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Input.UpdateSkillPattern(_skillPattern);

        await _patternRepository.UpdateAsync(_skillPattern, user);

        StatusMessage = "Pattern updated";

        return RedirectBack();
    }

    async Task<User?> InitializePageState(string skill, string slug)
    {
        var member = Viewer;
        var (user, organization) = member;
        var pattern = await _patternRepository.GetAsync(skill, slug, organization);

        if (pattern is null)
        {
            return null;
        }

        if (!await _permissions.CanEditAsync(member, pattern.Skill))
        {
            return null;
        }
        _skillPattern = pattern;
        Slug = slug;
        TestInput.Id = _skillPattern.Id;

        return user;
    }
}
