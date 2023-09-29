using Serious.Abbot.Skills;

namespace Serious.Abbot.Metadata;

/// <summary>
/// Represents a skill we intend to invoke.
/// </summary>
public interface IResolvedSkill
{
    /// <summary>
    /// The name of the skill the user attempted to invoke. In the case of aliases and
    /// user defined skills, it may differ from the actual skill that's invoked.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Any arguments to prepend to the user supplied arguments when calling the skill.
    /// </summary>
    /// <remarks>
    /// This is typically used for resolving aliases, lists, and user skills.
    /// </remarks>
    string Arguments { get; }

    /// <summary>
    /// Description of the skill.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The built-in skill to call.
    /// </summary>
    ISkill Skill { get; }

    /// <summary>
    /// The database skill id of the skill, if it's a user skill
    /// </summary>
    int? SkillId { get; }

    /// <summary>
    /// The data scope of the skill
    /// </summary>
    SkillDataScope Scope { get; }
}
