using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Triggers;

public class EditPageModel : SkillFeatureEditPageModel
{
    readonly ITriggerRepository _triggerRepository;
    readonly IScheduledSkillClient _scheduledSkillClient;
    readonly IPermissionRepository _permissions;

    public EditPageModel(
        ITriggerRepository triggerRepository,
        IScheduledSkillClient scheduledSkillClient,
        IPermissionRepository permissions)
    {
        _triggerRepository = triggerRepository;
        _scheduledSkillClient = scheduledSkillClient;
        _permissions = permissions;
    }

    [BindProperty]
    public string CronSchedule { get; set; } = null!;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string? TimeZoneId { get; set; }

    [BindProperty]
    public string? Arguments { get; set; }

    public TriggerViewModel Trigger { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string skill, string trigger, string triggerType)
    {
        var (_, dbTrigger) = await InitializePageState(skill, trigger, triggerType);
        if (dbTrigger is null)
        {
            return NotFound();
        }
        Description = dbTrigger.Description;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill, string trigger, string triggerType)
    {
        var (user, dbTrigger) = await InitializePageState(skill, trigger, triggerType);
        if (dbTrigger is null)
        {
            return NotFound();
        }

        if (dbTrigger is SkillScheduledTrigger scheduledTrigger)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            scheduledTrigger.CronSchedule = CronSchedule;
            scheduledTrigger.TimeZoneId = TimeZoneId;
            scheduledTrigger.Arguments = Arguments;
            StatusMessage = $"Trigger schedule updated to {scheduledTrigger.CronScheduleDescription}.";

            _scheduledSkillClient.ScheduleSkill(scheduledTrigger);
        }
        else
        {
            StatusMessage = "Trigger description updated!";
        }

        await _triggerRepository.UpdateTriggerDescriptionAsync(dbTrigger, Description, user);

        return RedirectBack();
    }

    async Task<(User, SkillTrigger?)> InitializePageState(string skillName, string triggerName, string triggerType)
    {
        var member = Viewer;
        var (user, organization) = member;

        SkillTrigger? trigger = triggerType switch
        {
            "http" => await _triggerRepository.GetSkillTriggerAsync<SkillHttpTrigger>(
                skillName,
                triggerName,
                organization),
            "schedule" => await _triggerRepository.GetSkillTriggerAsync<SkillScheduledTrigger>(
                skillName,
                triggerName,
                organization),
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

        Trigger = new TriggerViewModel(this, trigger, isEditing: true);

        return (user, trigger);
    }
}
