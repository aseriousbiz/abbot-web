using System;
using Serious.Abbot.Models;

namespace Serious.Abbot.Skills;

/// <summary>
/// Attribute to mark a class as a built-in skill.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SkillAttribute : Attribute
{
    /// <summary>
    /// Constructs a new <see cref="SkillAttribute"/>.
    /// </summary>
    public SkillAttribute()
    {
        Name = string.Empty;
    }

    /// <summary>
    /// Constructs a new <see cref="SkillAttribute"/> with the specified name.
    /// </summary>
    /// <param name="name">The skill name.</param>
    public SkillAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The name of the skill. This is what the user will type to invoke the skill.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A description of the skill that shows up when running the help skill.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether the skill is hidden when listing skills via the skills skill.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// Set this to require a specific feature flag be set in order to use the skill.
    /// </summary>
    public string? RequireFeatureFlag { get; set; }

    /// <summary>
    /// Set this to require a specific <see cref="PlanFeature"/> be available in the organization in order to use the skill.
    /// </summary>
    public PlanFeature RequirePlanFeature { get; set; }
}
