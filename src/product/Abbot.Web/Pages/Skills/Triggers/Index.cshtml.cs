using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Triggers;

public class IndexPageModel : SkillFeatureEditPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly IPermissionRepository _permissions;

    public IndexPageModel(
        ISkillRepository skillRepository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _permissions = permissions;
    }

    public bool CanRunSkill { get; private set; }

    public IReadOnlyList<TriggerViewModel> Triggers { get; private set; } = Array.Empty<TriggerViewModel>();

    public int SecretsCount { get; private set; }
    public int PatternsCount { get; private set; }
    public int TriggersCount { get; private set; }
    public int SignalSubscriptionsCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        var currentUser = Viewer;
        var dbSkill = await _skillRepository.GetAsync(skill, currentUser.Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }
        Skill = dbSkill;

        CanRunSkill = await _permissions.CanRunAsync(currentUser, dbSkill);

        Triggers = dbSkill
            .Triggers
            .Select(t => new TriggerViewModel(this, t, isReadonly: !CanRunSkill)).ToReadOnlyList();

        SecretsCount = dbSkill.Secrets.Count;
        PatternsCount = dbSkill.Patterns.Count;
        TriggersCount = dbSkill.Triggers.Count;
        SignalSubscriptionsCount = dbSkill.SignalSubscriptions.Count;

        return Page();
    }
}
