using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Secrets;

public class IndexPageModel : SkillFeatureEditPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly ISkillSecretRepository _skillSecretRepository;
    readonly IPermissionRepository _permissions;

    public IndexPageModel(
        ISkillRepository skillRepository,
        ISkillSecretRepository repository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _skillSecretRepository = repository;
        _permissions = permissions;
    }

    public IReadOnlyList<SkillSecret> Secrets { get; private set; } = Array.Empty<SkillSecret>();

    public bool CanEdit { get; private set; }
    public CodeLanguage Language { get; private set; } = CodeLanguage.CSharp;

    public int SecretsCount { get; private set; }
    public int PatternsCount { get; private set; }
    public int TriggersCount { get; private set; }
    public int SignalSubscriptionsCount { get; private set; }

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

        Language = dbSkill.Language;
        Secrets = await _skillSecretRepository.GetSecretsForSkillAsync(dbSkill);

        SecretsCount = Secrets.Count;
        PatternsCount = dbSkill.Patterns.Count;
        TriggersCount = dbSkill.Triggers.Count;
        SignalSubscriptionsCount = dbSkill.SignalSubscriptions.Count;

        return Page();
    }
}
