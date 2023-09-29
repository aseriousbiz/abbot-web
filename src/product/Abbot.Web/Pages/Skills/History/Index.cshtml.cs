using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.History;

public class IndexPage : CustomSkillPageModel
{
    readonly ISkillRepository _skillRepository;

    public IndexPage(ISkillRepository skillRepository)
    {
        _skillRepository = skillRepository;
    }

    public Skill Skill { get; private set; } = null!;

    public IReadOnlyList<SkillVersion> Versions { get; private set; } = Array.Empty<SkillVersion>();

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        Skill = await _skillRepository.GetWithVersionsAsync(skill, Organization)
                ?? throw new InvalidOperationException($"No skill with the name {skill} for this organization.");

        Versions = Skill.Versions.OrderByDescending(v => v.Created).ToReadOnlyList();
        return Page();
    }
}
