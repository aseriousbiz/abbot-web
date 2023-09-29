namespace Serious.Abbot.Entities;

/// <summary>
/// An entity that belongs to a skill.
/// </summary>
public interface ISkillChildEntity : IEntity
{
    /// <summary>
    /// The skill the entity belongs to.
    /// </summary>
    public Skill Skill { get; }
}
