using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Pages.Activity;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Collections;

namespace Serious.Abbot.Pages.Skills.Triggers.Activity;

public class IndexModel : ActivityPageBase
{
    readonly ITriggerRepository _triggerRepository;

    public IndexModel(
        ITriggerRepository triggerRepository,
        IAuditLogReader auditLog,
        IUserRepository userRepository) : base(auditLog, userRepository)
    {
        _triggerRepository = triggerRepository;
        ShowEventTypeFilter = false;
    }

    public SkillTrigger Trigger { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        string skill,
        string trigger,
        string triggerType,
        int? p = null,
        StatusFilter? filter = null,
        ActivityTypeFilter? type = null,
        string? range = null)
    {
        var organization = HttpContext.RequireCurrentOrganization();

        if (!organization.UserSkillsEnabled)
        {
            return NotFound();
        }

        SkillTrigger? dbTrigger = triggerType switch
        {
            "http" => await _triggerRepository.GetSkillTriggerAsync<SkillHttpTrigger>(
                skill,
                trigger,
                organization),
            "schedule" => await _triggerRepository.GetSkillTriggerAsync<SkillScheduledTrigger>(
                skill,
                trigger,
                organization),
            _ => null
        };

        if (dbTrigger is null)
        {
            return NotFound();
        }

        Trigger = dbTrigger;

        await GetAsync(
            p ?? 1,
            filter ?? StatusFilter.All,
            type ?? ActivityTypeFilter.All,
            range ?? string.Empty);
        return Page();
    }

    protected override Task<IPaginatedList<AuditEventBase>> LoadActivityEventsAsync(
        Organization organization,
        DateTime? minDate,
        DateTime? maxDate)
    {
        return AuditLog.GetAuditEventsForSkillTriggerAsync(
            Trigger,
            PageNumber,
            PageCount.GetValueOrDefault(),
            Filter,
            minDate,
            maxDate);
    }
}
