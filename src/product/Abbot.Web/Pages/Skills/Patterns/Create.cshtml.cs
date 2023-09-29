using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Patterns;

public class CreatePageModel : SkillFeatureEditPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly IPatternRepository _patternRepository;
    readonly IPermissionRepository _permissions;
    Skill _skill = null!;

    public CreatePageModel(
        IPatternRepository patternRepository,
        ISkillRepository skillRepository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _patternRepository = patternRepository;
        _permissions = permissions;
    }

    [BindProperty]
    public PatternInputModel Input { get; set; } = new();

    public PatternTestInputModel TestInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        var user = await InitializePageState(skill);
        if (user is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill)
    {
        var user = await InitializePageState(skill);
        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _patternRepository.CreateAsync(
            Input.Name,
            Input.Pattern,
            Input.PatternType,
            Input.CaseSensitive,
            _skill,
            user,
            Input.Enabled,
            Input.AllowExternalCallers);

        return RedirectBack();
    }

    async Task<User?> InitializePageState(string skill)
    {
        var member = Viewer;
        var (user, organization) = member;

        var dbSkill = await _skillRepository.GetAsync(skill, organization);
        if (dbSkill is null)
        {
            return null;
        }

        if (!await _permissions.CanEditAsync(member, dbSkill))
        {
            return null;
        }

        _skill = dbSkill;

        return user;
    }
}
