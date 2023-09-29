using Serious.Abbot.Entities;

namespace Serious.Abbot.Messages;

/// <summary>
/// Used to uniquely identify a skill assembly.
/// </summary>
public interface ICompiledSkillIdentifier : IOrganizationIdentifier
{
    /// <summary>
    /// The id of the skill
    /// </summary>
    int SkillId { get; }

    /// <summary>
    /// The name of the skill.
    /// </summary>
    string SkillName { get; }

    /// <summary>
    /// The cache key for the skill.
    /// </summary>
    string CacheKey { get; }

    CodeLanguage Language { get; }
}
