using System.IO;
using System.Threading.Tasks;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Infrastructure.Compilation;

/// <summary>
/// Used to compile and cache code.
/// </summary>
public interface ICachingCompilerService
{
    /// <summary>
    /// Checks to see if the code is already in the cache.
    /// </summary>
    /// <param name="organizationIdentifier">Identifies the chat platform for the organization that owns the code.</param>
    /// <param name="code">The code to check for existence in the cache.</param>
    Task<bool> ExistsAsync(IOrganizationIdentifier organizationIdentifier, string code);

    /// <summary>
    /// Compiles the specified code and stores it in the cache.
    /// </summary>
    /// <param name="organizationIdentifier">Identifies the platform that hosts the compiled skill</param>
    /// <param name="language"></param>
    /// <param name="code">The code to compile</param>
    Task<ICompilationResult> CompileAsync(IOrganizationIdentifier organizationIdentifier, CodeLanguage language, string code);

    /// <summary>
    /// Retrieves the cached assembly (or symbols) from the cache.
    /// </summary>
    /// <param name="compilationRequest">The type of compilation to request.</param>
    Task<Stream> GetCachedAssemblyStreamAsync(CompilationRequest compilationRequest);
}
