using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Subscriptions;

public class IndexPageModel : SkillFeatureEditPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly IPermissionRepository _permissions;

    public IReadOnlyList<SignalSubscription> Subscriptions { get; private set; } = Array.Empty<SignalSubscription>();

    public bool CanEdit { get; private set; }

    public int SecretsCount { get; private set; }
    public int PatternsCount { get; private set; }
    public int TriggersCount { get; private set; }
    public int SignalSubscriptionsCount { get; private set; }

    public IndexPageModel(
        ISkillRepository skillRepository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _permissions = permissions;
    }

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        var currentMember = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentMember.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }
        Skill = dbSkill;
        CanEdit = await _permissions.CanEditAsync(currentMember, dbSkill);

        SecretsCount = dbSkill.Secrets.Count;
        PatternsCount = dbSkill.Patterns.Count;
        TriggersCount = dbSkill.Triggers.Count;
        SignalSubscriptionsCount = dbSkill.SignalSubscriptions.Count;

        Subscriptions = dbSkill.SignalSubscriptions;
        return Page();
    }
}
