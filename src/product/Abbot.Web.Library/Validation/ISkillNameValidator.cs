using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Validation;

/// <summary>
/// Used to validate a skill name.
/// </summary>
public interface ISkillNameValidator
{
    /// <summary>
    /// Determines whether the provided name is a unique skill name across custom skills, built-in skills,
    /// list skills, and aliases.
    /// </summary>
    /// <param name="name">Name of the skill to test.</param>
    /// <param name="id">Id of the current entity.</param>
    /// <param name="type">The type of skill to test.</param>
    /// <param name="organization">The organization to check.</param>
    /// <returns>True if the name is unique or matches the name of the current entity specified by id and type.</returns>
    Task<UniqueNameResult> IsUniqueNameAsync(string name, int id, string type, Organization organization);
}
