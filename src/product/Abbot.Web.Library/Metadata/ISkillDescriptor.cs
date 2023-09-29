namespace Serious.Abbot.Metadata;

/// <summary>
/// Describes anything that's invoked like a skill such as an Alias, Skill, BuiltInSkill, or UserList.
/// </summary>
public interface ISkillDescriptor
{
    /// <summary>
    /// The name of the skill.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The description of the skill.
    /// </summary>
    string Description { get; }
}
