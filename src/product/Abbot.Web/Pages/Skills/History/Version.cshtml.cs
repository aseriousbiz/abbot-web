using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.History;

public class VersionPage : CustomSkillPageModel
{
    readonly ISkillRepository _skillRepository;

    public VersionPage(ISkillRepository skillRepository)
    {
        _skillRepository = skillRepository;
    }

    public string SkillName { get; private set; } = null!;
    public int Version { get; private set; }

    public VersionHistoryItem Snapshot { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string skill, int version)
    {
        SkillName = skill;
        Version = version;
        var dbSkill = await _skillRepository.GetWithVersionsAsync(skill, Organization)
                      ?? throw new InvalidOperationException($"No skill with the name {skill}.");

        var versionHistory = new VersionHistory(dbSkill);
        Snapshot = versionHistory.GetVersionSnapshot(version);
        return Page();
    }
}
