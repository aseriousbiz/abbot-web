using System;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

/// <summary>
/// Contains updates to make to a skill. Properties that are not changed remain null.
/// </summary>
public class SkillUpdateModel
{
    string? _name;

    public string? Name
    {
        get => _name;
        set => _name = string.IsNullOrEmpty(value) ? null : value.ToLowerInvariant();
    }
    public string? UsageText { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool? Restricted { get; set; }
    public bool? Enabled { get; set; }
    public SkillDataScope? Scope { get; set; }

    /// <summary>
    /// The original skill name if the name was changed in this skill version. Useful for our audit logs.
    /// </summary>
    public string? OriginalSkillName { get; private set; }

    /// <summary>
    /// The ID of the package version that's the source for this skill. This is set if the skill is created
    /// from a package.
    /// </summary>
    public int? SourcePackageVersionId { get; set; }

    public void ApplyChanges(Skill skill)
    {
        if (Code is not null)
        {
            skill.CacheKey = SkillCompiler.ComputeCacheKey(Code);
        }

        if (Name is not null)
        {
            OriginalSkillName = skill.Name;
            skill.Name = Name;
        }

        skill.UsageText = UsageText ?? skill.UsageText;
        skill.Code = Code ?? skill.Code;
        skill.Description = Description ?? skill.Description;
        skill.SourcePackageVersionId = SourcePackageVersionId ?? skill.SourcePackageVersionId;
        skill.Restricted = Restricted ?? skill.Restricted;
        skill.Enabled = Enabled ?? skill.Enabled;
        skill.Scope = Scope ?? skill.Scope;
    }

    /// <summary>
    /// We have special logging to apply if the only thing we changed was the Enabled property.
    /// </summary>
    public bool OnlyChangedEnabled => Enabled is not null
                                      && Code is null
                                      && Name is null
                                      && UsageText is null
                                      && Description is null
                                      && SourcePackageVersionId is null
                                      && Restricted is null
                                      && Scope is null;

    public SkillVersion ToVersionSnapshot(Skill original)
    {
        return new()
        {
            SkillId = original.Id,
            Skill = original,
            Name = Name is null || original.Name.Equals(Name, StringComparison.Ordinal) ? null : original.Name, // Only store name if it's changed.
            UsageText = UsageText is null || original.UsageText.Equals(UsageText, StringComparison.Ordinal) ? null : original.UsageText,
            Code = Code is null || original.Code.Equals(Code, StringComparison.Ordinal) ? null : original.Code,
            Description = Description is null || original.Description.Equals(Description, StringComparison.Ordinal) ? null : original.Description,
            Restricted = Restricted is null || original.Restricted.Equals(Restricted) ? null : original.Restricted,
            Created = original.Modified,
            CreatorId = original.ModifiedById,
            Creator = original.ModifiedBy,
            Scope = Scope is null || original.Scope == Scope ? null : original.Scope,
        };
    }
}
