using System;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

/// <summary>
/// A view model for a skill version snapshot. This is used to render it on a page.
/// </summary>
public class VersionHistoryItem
{
    public VersionHistoryItem(Skill skill, SkillUpdateModel changeSet, SkillVersion version)
    {
        Name = changeSet.Name ?? skill.Name;
        Language = skill.Language;
        Code = changeSet.Code ?? skill.Code;
        Description = changeSet.Description ?? skill.Description;
        UsageText = changeSet.UsageText ?? skill.UsageText;
        Modified = version.Created;
        ModifiedBy = version.Creator;
    }

    public string Name { get; }
    public CodeLanguage Language { get; }
    public string Code { get; }
    public string Description { get; }
    public string UsageText { get; }

    public User ModifiedBy { get; }
    public DateTime Modified { get; }
}
