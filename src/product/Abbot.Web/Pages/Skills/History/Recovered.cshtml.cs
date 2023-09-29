using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.History;

public class RecoveredPage : CustomSkillPageModel
{
    readonly ISkillRepository _skillRepository;

    public RecoveredPage(ISkillRepository skillRepository)
    {
        _skillRepository = skillRepository;
    }

    public string SkillName { get; set; } = null!;

    public int SkillId { get; set; }

    public CodeLanguage Language { get; set; }

    public async Task<IActionResult> OnGet(string skill)
    {
        if (!Organization.UserSkillsEnabled)
        {
            return NotFound();
        }

        var dbSkill = await _skillRepository.GetAsync(skill, Organization);
        if (dbSkill is null)
        {
            return NotFound();
        }

        SkillId = dbSkill.Id;
        SkillName = dbSkill.Name;
        Language = dbSkill.Language;

        return Page();
    }
}
