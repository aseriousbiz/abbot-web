using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Secrets;

public class DeletePageModel : SkillFeatureEditPageModel
{
    readonly ISkillSecretRepository _skillSecretRepository;
    readonly IPermissionRepository _permissions;

    public DeletePageModel(
        ISkillSecretRepository skillSecretRepository,
        IPermissionRepository permissions)
    {
        _skillSecretRepository = skillSecretRepository;
        _permissions = permissions;
    }

    public string SecretName { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string skill, string name)
    {
        var (_, secret) = await InitializePageState(skill, name);
        if (secret is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill, string name)
    {
        var (user, secret) = await InitializePageState(skill, name);
        if (secret is null)
        {
            return NotFound();
        }

        await _skillSecretRepository.DeleteAsync(secret, user);

        return RedirectBack();
    }

    async Task<(User, SkillSecret?)> InitializePageState(string skill, string name)
    {
        var member = Viewer;
        var (user, organization) = member;
        var skillSecret = await _skillSecretRepository.GetAsync(skill, name, organization);

        if (skillSecret is null)
        {
            return (user, null);
        }

        if (!await _permissions.CanEditAsync(member, skillSecret.Skill))
        {
            return (user, null);
        }


        SecretName = skillSecret.Name;

        return (user, skillSecret);
    }
}
