using Serious.Abbot.Entities;
using Serious.Abbot.Skills;

namespace Serious.Abbot.Metadata;

/// <summary>
/// Represents a built-in <see cref="ISkill"/> we intend to invoke. When calling user skills, the
/// <see cref="Skill"/> property will be <see cref="RemoteSkillCallSkill"/>.
/// </summary>
public class ResolvedSkill : IResolvedSkill
{
    /// <summary>
    /// Constructs a resolved skill.
    /// </summary>
    /// <param name="name">The name of the <see cref="ISkill"/>.</param>
    /// <param name="arguments">The arguments to pass to the skill.</param>
    /// <param name="description">The description of the skill (or list, or alias)</param>
    /// <param name="skill">The built-in skill to call.</param>
    public ResolvedSkill(string name, string arguments, string description, ISkill skill)
    {
        Name = name;
        Arguments = arguments.Trim();
        Description = description;
        Skill = skill;
    }

    /// <summary>
    /// Any arguments to prepend to the user supplied arguments.
    /// </summary>
    /// <remarks>
    /// This is typically used for resolving aliases and user skills.
    /// </remarks>
    public string Arguments { get; }

    /// <summary>
    /// A description of the resolved skill.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// The built-in skill to call.
    /// </summary>
    public ISkill Skill { get; }

    /// <summary>
    /// The name of the skill to call.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The database skill id of the skill, if it's a user skill
    /// </summary>
    public int? SkillId { get; private set; }

    public SkillDataScope Scope { get; private set; }

    /// <summary>
    /// Create a new resolved skill with the specified name and description. This supports aliases and
    /// user skills.
    /// </summary>
    /// <param name="name">Name of the skill or alias.</param>
    /// <param name="description">The description of the skill or alias.</param>
    public ResolvedSkill WithNameAndDescription(string name, string description)
    {
        Name = name;
        Description = description.DefaultIfEmpty(Description);
        return this;
    }

    public ResolvedSkill WithSkill(Skill skill)
    {
        Name = skill.Name;
        Description = skill.Description;
        SkillId = skill.Id;
        Scope = skill.Scope;
        return this;
    }
}
