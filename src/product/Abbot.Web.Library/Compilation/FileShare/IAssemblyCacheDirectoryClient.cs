using System.Collections.Generic;

namespace Serious.Abbot.Storage.FileShare;

/// <summary>
/// Client to the directory that contains the compiled skill assemblies.
/// </summary>
public interface IAssemblyCacheDirectoryClient
{
    /// <summary>
    /// Get a writeable client for a compiled assembly within the cache directory.
    /// </summary>
    /// <param name="cacheKey">The cache key for the skill assembly. Typically a hash of the code.</param>
    /// <returns>An <see cref="IAssemblyClient"/> used to manipulate the assembly.</returns>
    IAssemblyClient GetAssemblyClient(string cacheKey);

    /// <summary>
    /// Retrieves the list of cached assemblies in this directory.
    /// </summary>
    IAsyncEnumerable<IAssemblyClient> GetCachedAssemblies();
}
