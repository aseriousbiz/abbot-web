using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Secrets;

public class CreatePageModel : SkillFeatureEditPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly ISkillSecretRepository _skillSecretRepository;
    readonly IPermissionRepository _permissions;
    Skill _skill = null!;

    public CreatePageModel(
        ISkillSecretRepository skillSecretRepository,
        ISkillRepository skillRepository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _skillSecretRepository = skillSecretRepository;
        _permissions = permissions;
    }

    [BindProperty]
    public SecretInputModel Input { get; set; } = new SecretInputModel();

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

        await _skillSecretRepository.CreateAsync(
            Input.Name,
            Input.Value,
            Input.Description,
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
