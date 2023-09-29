using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Triggers;

public class DeletePageModel : SkillFeatureEditPageModel
{
    readonly IPermissionRepository _permissions;
    readonly ITriggerRepository _triggerRepository;

    public DeletePageModel(
        ITriggerRepository triggerRepository,
        IPermissionRepository permissions)
    {
        _triggerRepository = triggerRepository;
        _permissions = permissions;
    }

    public TriggerViewModel Trigger { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string skill, string trigger, string triggerType)
    {
        var (_, dbTrigger) = await InitializePageState(skill, trigger, triggerType);
        if (dbTrigger is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill, string trigger, string triggerType)
    {
        var (user, dbTrigger) = await InitializePageState(skill, trigger, triggerType);
        if (dbTrigger is null)
        {
            return NotFound();
        }

        await _triggerRepository.DeleteTriggerAsync(dbTrigger, user);

        return RedirectBack();
    }

    async Task<(User, SkillTrigger?)> InitializePageState(string skillName, string triggerName, string triggerType)
    {
        var member = Viewer;
        var (user, organization) = member;

        SkillTrigger? trigger = triggerType switch
        {
            "http" => await _triggerRepository.GetSkillTriggerAsync<SkillHttpTrigger>(skillName, triggerName,
                organization),
            "schedule" => await _triggerRepository.GetSkillTriggerAsync<SkillScheduledTrigger>(skillName,
                triggerName, organization),
            _ => null
        };

        if (trigger is null)
        {
            return (user, null);
        }

        if (!await _permissions.CanRunAsync(member, trigger.Skill))
        {
            return (user, null);
        }

        Trigger = new TriggerViewModel(this, trigger, isDeleting: true);

        return (user, trigger);
    }
}
