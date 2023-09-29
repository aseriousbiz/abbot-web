using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Validation;

/// <summary>
/// Used to validate a pattern.
/// </summary>
public interface IPatternValidator
{
    /// <summary>
    /// Determines whether the pattern name is unique across all the pattern for the skill.
    /// </summary>
    /// <param name="name">Name of the pattern to test.</param>
    /// <param name="id">Id of the current pattern.</param>
    /// <param name="skill">Name of the skill the pattern belongs to.</param>
    /// <param name="organization">The organization the skill belongs to.</param>
    /// <returns>True if the name is unique for the skill.</returns>
    Task<bool> IsUniqueNameAsync(string name, int? id, string skill, Organization organization);

    /// <summary>
    /// Determines whether the pattern is valid for the pattern type.
    /// </summary>
    /// <remarks>This only does something interesting for <see cref="PatternType.RegularExpression"/> patterns.</remarks>
    /// <param name="pattern">The pattern to test.</param>
    /// <param name="patternType">The pattern type.</param>
    /// <returns>True if the pattern is valid.</returns>
    bool IsValidPattern(string pattern, PatternType patternType);
}
