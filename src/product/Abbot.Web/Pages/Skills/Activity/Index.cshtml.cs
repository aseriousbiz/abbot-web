using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Pages.Activity;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Collections;

namespace Serious.Abbot.Pages.Skills.Activity;

public class IndexModel : ActivityPageBase
{
    readonly ISkillRepository _skillRepository;

    public IndexModel(
        ISkillRepository skillRepository,
        IAuditLogReader auditLog,
        IUserRepository userRepository) : base(auditLog, userRepository)
    {
        _skillRepository = skillRepository;
        ShowEventTypeFilter = false;
        ShowSkillEventTypeFilter = true;
    }

    public Skill Skill { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(
        string skill,
        int? p = null,
        StatusFilter? filter = null,
        SkillEventFilter? type = null,
        string? range = null)
    {
        var organization = HttpContext.RequireCurrentOrganization();
        var dbSkill = await _skillRepository.GetAsync(skill, organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        Skill = dbSkill;
        await GetAsync(
            p ?? 1,
            filter ?? StatusFilter.All,
            (ActivityTypeFilter)(type ?? SkillEventFilter.All),
            range ?? string.Empty);
        return Page();
    }

    protected override Task<IPaginatedList<AuditEventBase>> LoadActivityEventsAsync(
        Organization organization,
        DateTime? minDate,
        DateTime? maxDate)
    {
        return AuditLog.GetAuditEventsForSkillAsync(
            Skill,
            PageNumber,
            PageCount.GetValueOrDefault(),
            Filter,
            (SkillEventFilter)Type,
            minDate,
            maxDate);
    }
}
