using System;
using System.Collections.Generic;

namespace Serious.Abbot.Storage.FileShare;

public class AssemblyCacheDirectoryClient : IAssemblyCacheDirectoryClient
{
    readonly IShareDirectoryClient _cacheDirectory;

    public AssemblyCacheDirectoryClient(IShareDirectoryClient cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

    public IAssemblyClient GetAssemblyClient(string cacheKey)
    {
        var pdbFileName = $"{cacheKey}.pdb";
        var assemblyFileClient = _cacheDirectory.GetFileClient(cacheKey);
        var symbolsFileClient = _cacheDirectory.GetFileClient(pdbFileName);
        return _cacheDirectory.CreateAssemblyClient(assemblyFileClient, symbolsFileClient);
    }

    public async IAsyncEnumerable<IAssemblyClient> GetCachedAssemblies()
    {
        var files = _cacheDirectory.GetFilesAndDirectoriesAsync();
        await foreach (var file in files)
        {
            if (!file.Name.EndsWith(".pdb", StringComparison.Ordinal))
            {
                yield return GetAssemblyClient(file.Name);
            }
        }
    }
}
