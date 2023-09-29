using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository that manages patterns for skills. Used to create, retrieve, update, and delete
/// triggers.
/// </summary>
public interface IPatternRepository
{
    /// <summary>
    /// Retrieve a pattern by skill, name, and organization combination.
    /// </summary>
    /// <param name="skill">The skill the pattern belongs to.</param>
    /// <param name="slug">The slug of the pattern.</param>
    /// <param name="organization">The organization the pattern belongs to.</param>
    Task<SkillPattern?> GetAsync(string skill, string slug, Organization organization);

    /// <summary>
    /// Retrieves all the <see cref="SkillPattern"/> instances in the organization.
    /// </summary>
    /// <param name="organization">The organization to retrieve skill patterns for.</param>
    /// <param name="enabledPatternsOnly">Only return patterns that are enabled.</param>
    /// <returns>All the <see cref="SkillPattern"/> instances for the specified organization.</returns>
    Task<IReadOnlyList<SkillPattern>> GetAllAsync(Organization organization, bool enabledPatternsOnly = false);

    /// <summary>
    /// Retrieves all the <see cref="SkillPattern"/>s for the specified <see cref="Skill"/>.
    /// </summary>
    /// <param name="skill">The skill the patterns belong to.</param>
    /// <returns>All the <see cref="SkillPattern"/> instances for the specified skill.</returns>
    Task<IReadOnlyList<SkillPattern>> GetAllForSkillAsync(Skill skill);

    /// <summary>
    /// Creates a pattern with the specified values.
    /// </summary>
    /// <param name="name">The name of the pattern.</param>
    /// <param name="pattern">The pattern to match.</param>
    /// <param name="patternType">The <see cref="PatternType"/> of the pattern.</param>
    /// <param name="caseSensitive">Whether the pattern should be applied in a case sensitive manner or not.</param>
    /// <param name="skill">The <see cref="Skill"/> the pattern belongs to.</param>
    /// <param name="creator">The creator of the pattern.</param>
    /// <param name="enabled">Whether the pattern should be enabled or not.</param>
    /// <param name="allowExternalCallers">Whether the pattern matches messages from external users.</param>
    Task<SkillPattern> CreateAsync(
        string name,
        string pattern,
        PatternType patternType,
        bool caseSensitive,
        Skill skill,
        User creator,
        bool enabled,
        bool allowExternalCallers = false);

    /// <summary>
    /// Updates the specified pattern.
    /// </summary>
    /// <param name="pattern">The updated pattern.</param>
    /// <param name="modifier">The user making the change.</param>
    Task UpdateAsync(SkillPattern pattern, User modifier);

    /// <summary>
    /// Delete the specified pattern.
    /// </summary>
    /// <param name="pattern">The pattern to delete.</param>
    /// <param name="deleteBy">The user deleting the pattern.</param>
    Task DeleteAsync(SkillPattern pattern, User deleteBy);
}
