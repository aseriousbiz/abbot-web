using Serious.Abbot.Models;
using Serious.Abbot.Skills;

namespace Serious.Abbot.Metadata;

/// <summary>
/// Used to describe a built-in skill.
/// </summary>
public interface IBuiltinSkillDescriptor : ISkillDescriptor
{
    /// <summary>
    /// The actual skill.
    /// </summary>
    ISkill Skill { get; }

    /// <summary>
    /// Whether it's hidden or not from the help command.
    /// </summary>
    bool Hidden { get; }

    /// <summary>
    /// Gets the feature flag, if any, that determines if users can see this skill.
    /// </summary>
    string? FeatureFlag { get; }

    /// <summary>
    /// Gets the <see cref="PlanFeature"/>, if any, that determines if users can see this skill.
    /// </summary>
    PlanFeature? PlanFeature { get; }
}
