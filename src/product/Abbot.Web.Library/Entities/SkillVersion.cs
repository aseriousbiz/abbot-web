using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Humanizer;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a snapshot version change set of a <see cref="Skill"/>.
/// When a skill is updated, this version contains the old values of
/// any changed properties.
/// </summary>
public class SkillVersion : EntityBase<SkillVersion>, ISkillChildEntity
{
    /// <summary>
    /// The old name of the skill if it changed.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The old description of the skill if it changed.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The old usage text of the skill if it changed.
    /// </summary>
    public string? UsageText { get; set; }

    /// <summary>
    /// The old code of the skill if it changed.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// The Id of the <see cref="Skill"/> this is a version of.
    /// </summary>
    public int SkillId { get; set; }

    /// <summary>
    /// The <see cref="Skill"/> this is a version of.
    /// </summary>
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="User"/> that was the last modifier before
    /// this change. In other words, the user that created this version
    /// of the skill.
    /// </summary>
    public int CreatorId { get; set; }

    /// <summary>
    /// Whether or not the skill is subject to access controls or not.
    /// <c>true</c> if permissions are required, <c>false</c> if anyone can call it. Null if not changed.
    /// </summary>
    public bool? Restricted { get; set; }

    /// <summary>
    /// The <see cref="User"/> that was the previous modifier before
    /// this change. In other words, the user that created this version
    /// of the skill.
    /// </summary>
    public User Creator { get; set; } = null!;

    /// <summary>
    /// The scope of the skill data item - per skill, per user, per conversation...
    /// </summary>
    [Column(TypeName = "text")]
    public SkillDataScope? Scope { get; set; } = null!;

    /// <summary>
    /// Returns the type of change this version represents.
    /// </summary>
    public string GetChangeDescription()
    {
        var changeDescription = ChangedProperties.Humanize();

        return changeDescription.Length == 0
            ? "Nothing changed"
            : $"{changeDescription} changed.";
    }

    public IEnumerable<string> ChangedProperties
    {
        get {
            if (Name is not null)
            {
                yield return "Name";
            }

            if (Description is not null)
            {
                yield return "Description";
            }

            if (Restricted is not null)
            {
                yield return "Restricted";
            }

            if (UsageText is not null)
            {
                yield return "Usage";
            }

            if (Code is not null)
            {
                yield return "Code";
            }

            if (Scope is not null)
            {
                yield return "Scope";
            }
        }
    }
}
