using System.Threading.Tasks;
using Serious.Abbot.Execution;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Functions.Cache;

/// <summary>
/// Retrieves a compiled skill based on the skill id and cache key.
/// </summary>
public interface ICompilationCache
{
    /// <summary>
    /// Retrieves a compiled skill based on the skill id and the cache key.
    /// </summary>
    /// <param name="skillAssemblyIdentifier">Uniquely identifies a skill assembly</param>
    /// <returns>The compiled skill from the cache or null if it's not in the cache.</returns>
    Task<ICompiledSkill> GetCompiledSkillAsync(ICompiledSkillIdentifier skillAssemblyIdentifier);
}
