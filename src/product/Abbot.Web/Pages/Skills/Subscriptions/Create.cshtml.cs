using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Subscriptions;

public class CreatePageModel : SkillFeatureEditPageModel
{
    readonly ISignalRepository _signalRepository;
    readonly ISkillRepository _skillRepository;
    readonly IPermissionRepository _permissions;
    Skill _skill = null!;

    public CreatePageModel(
        ISignalRepository signalRepository,
        ISkillRepository skillRepository,
        IPermissionRepository permissions)
    {
        _signalRepository = signalRepository;
        _skillRepository = skillRepository;
        _permissions = permissions;
    }

    [BindProperty]
    public SignalSubscriptionInputModel Input { get; set; } = new() { PatternType = PatternType.ExactMatch };

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

        await _signalRepository.CreateAsync(
            Input.Name,
            Input.Pattern,
            Input.PatternType,
            Input.CaseSensitive,
            _skill,
            user);
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
