using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Patterns;

public class IndexPageModel : SkillFeatureEditPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly IPatternRepository _repository;
    readonly IPermissionRepository _permissions;

    public IReadOnlyList<SkillPattern> Patterns { get; private set; } = Array.Empty<SkillPattern>();

    public bool CanEdit { get; private set; }

    public IndexPageModel(
        ISkillRepository skillRepository,
        IPatternRepository repository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _repository = repository;
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

        Patterns = await _repository.GetAllForSkillAsync(dbSkill);
        return Page();
    }
}
