using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Patterns;

public class DeletePageModel : SkillFeatureEditPageModel
{
    readonly IPatternRepository _patternRepository;
    readonly IPermissionRepository _permissions;

    public DeletePageModel(
        IPatternRepository patternRepository,
        IPermissionRepository permissions)
    {
        _patternRepository = patternRepository;
        _permissions = permissions;
    }

    public string PatternName { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        string skill,
        string slug)
    {
        var (_, pattern) = await InitializePageState(skill, slug);
        if (pattern is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string skill,
        string slug)
    {
        var (user, pattern) = await InitializePageState(skill, slug);
        if (pattern is null)
        {
            return NotFound();
        }

        await _patternRepository.DeleteAsync(pattern, user);

        return RedirectBack();
    }

    async Task<(User, SkillPattern?)> InitializePageState(string skill, string slug)
    {
        var member = Viewer;
        var (user, organization) = member;
        var pattern = await _patternRepository.GetAsync(skill, slug, organization);

        if (pattern is null)
        {
            return (user, null);
        }

        if (!await _permissions.CanEditAsync(member, pattern.Skill))
        {
            return (user, null);
        }

        PatternName = pattern.Name;

        return (user, pattern);
    }
}
