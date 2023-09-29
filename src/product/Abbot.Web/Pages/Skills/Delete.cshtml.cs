using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills;

public class DeletePage : CustomSkillPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly IPermissionRepository _permissions;

    public DeletePage(
        ISkillRepository skillRepository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _permissions = permissions;
    }

    public Skill Skill { get; private set; } = null!;

    public bool CanDelete { get; private set; }

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        var (dbSkill, _) = await InitializeState(skill);

        if (dbSkill is null)
        {
            // This only happens if we have an id, but it doesn't match a skill.
            return NotFound();
        }

        Skill = dbSkill;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill)
    {
        var (dbSkill, user) = await InitializeState(skill);

        if (dbSkill is null or { Package: { } } || !CanDelete)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _skillRepository.RemoveAsync(dbSkill, user);

        StatusMessage = "Skill deleted";

        return RedirectToPage("Index");
    }

    async Task<(Skill?, User)> InitializeState(string skill)
    {
        var member = Viewer;
        var (user, organization) = member;
        var dbSkill = await _skillRepository.GetAsync(skill, organization);
        if (dbSkill is not null)
        {
            CanDelete = await _permissions.CanEditAsync(member, dbSkill);
        }

        return (dbSkill, user);
    }
}
