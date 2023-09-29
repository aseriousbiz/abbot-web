using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Secrets;

public class EditPageModel : SkillFeatureEditPageModel
{
    readonly ISkillSecretRepository _skillSecretRepository;
    readonly IPermissionRepository _permissions;
    SkillSecret _skillSecret = null!;

    public EditPageModel(
        ISkillSecretRepository skillSecretRepository,
        IPermissionRepository permissions)
    {
        _skillSecretRepository = skillSecretRepository;
        _permissions = permissions;
    }

    [BindProperty]
    public SecretInputModel Input { get; set; } = new();

    public string SecretName { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string skill, string name)
    {
        var user = await InitializePageState(skill, name);
        if (user is null)
        {
            return NotFound();
        }
        Input.Name = _skillSecret.Name;
        Input.Description = _skillSecret.Description;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill, string name)
    {
        var user = await InitializePageState(skill, name);
        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!_skillSecret.Name.Equals(Input.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cheeky bastard is trying to tamper with the form.");
        }

        await _skillSecretRepository.UpdateAsync(
            _skillSecret,
            Input.Value,
            Input.Description,
            user);

        return RedirectBack();
    }

    async Task<User?> InitializePageState(string skill, string name)
    {
        var member = Viewer;
        var (user, organization) = member;
        var skillSecret = await _skillSecretRepository.GetAsync(skill, name, organization);

        if (skillSecret is null)
        {
            return null;
        }

        if (!await _permissions.CanEditAsync(member, skillSecret.Skill))
        {
            return null;
        }
        _skillSecret = skillSecret;

        SecretName = _skillSecret.Name;
        return user;
    }
}
