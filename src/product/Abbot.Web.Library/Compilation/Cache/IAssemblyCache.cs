using System.IO;
using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Compilation;

/// <summary>
/// Reads and writes a compiled skill to the assembly cache.
/// </summary>
public interface IAssemblyCache
{
    /// <summary>
    /// Checks to see if the assembly is already in the cache.
    /// </summary>
    /// <param name="organizationIdentifier">Identifies the chat platform for the organization that owns the code.</param>
    /// <param name="cacheKey">The cache key for the code.</param>
    Task<bool> AssemblyExistsAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey);

    /// <summary>
    /// Downloads a compiled skill assembly as a stream.
    /// </summary>
    /// <param name="organizationIdentifier">The skill to download.</param>
    /// <param name="cacheKey">The cache key for the skill code.</param>
    /// <returns>The compiled skill assembly from the cache or a null stream if it's not in the cache.</returns>
    Task<Stream> DownloadAssemblyAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey);

    /// <summary>
    /// Downloads a compiled skill's symbols as a stream.
    /// </summary>
    /// <param name="organizationIdentifier">The skill to download.</param>
    /// <param name="cacheKey">The cache key for the skill code.</param>
    /// <returns>The compiled skill assembly from the cache or a null stream if it's not in the cache.</returns>
    Task<Stream> DownloadSymbolsAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey);

    /// <summary>
    /// Writes a compiled skill to our assembly cache which is stored in an Azure File SHare.
    /// </summary>
    /// <param name="organizationIdentifier">Uniquely identifies a skill</param>
    /// <param name="skillCompilation">The compiled skill to write</param>
    Task WriteToCacheAsync(IOrganizationIdentifier organizationIdentifier, ISkillCompilation skillCompilation);
}
