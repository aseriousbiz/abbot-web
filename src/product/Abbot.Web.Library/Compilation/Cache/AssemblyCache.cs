using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Storage.FileShare;
using Serious.Logging;

namespace Serious.Abbot.Compilation;

/// <summary>
/// Writes a compiled skill to the assembly cache.
/// </summary>
public class AssemblyCache : IAssemblyCache
{
    static readonly ILogger<AssemblyCache> Log = ApplicationLoggerFactory.CreateLogger<AssemblyCache>();

    readonly IAssemblyCacheClient _assemblyCacheClient;

    public AssemblyCache(IAssemblyCacheClient assemblyCacheClient)
    {
        _assemblyCacheClient = assemblyCacheClient;
    }

    public async Task<bool> AssemblyExistsAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey)
    {
        var assemblyClient = await GetAssemblyClient(organizationIdentifier, cacheKey);
        try
        {
            return await assemblyClient.ExistsAsync();
        }
        catch (Exception e)
        {
            Log.ExceptionCheckingAssembly(e, assemblyClient.Name, cacheKey, organizationIdentifier.PlatformId, organizationIdentifier.PlatformType);
            return false;
        }
    }

    public async Task<Stream> DownloadAssemblyAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey)
    {
        try
        {
            var assemblyClient = await GetAssemblyClient(organizationIdentifier, cacheKey);
            if (await assemblyClient.ExistsAsync())
            {
                return await assemblyClient.DownloadAssemblyAsync();
            }
        }
        catch (Exception e)
        {
            Log.ExceptionDownloadingSkillAssembly(e, cacheKey, organizationIdentifier.PlatformId, organizationIdentifier.PlatformType);
        }
        return Stream.Null;
    }

    public async Task<Stream> DownloadSymbolsAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey)
    {
        try
        {
            var assemblyClient = await GetAssemblyClient(organizationIdentifier, cacheKey);
            if (await assemblyClient.SymbolsExistAsync())
            {
                return await assemblyClient.DownloadSymbolsAsync();
            }
        }
        catch (Exception e)
        {
            Log.ExceptionDownloadingSymbols(e, cacheKey, organizationIdentifier.PlatformId, organizationIdentifier.PlatformType);
        }
        return Stream.Null;
    }

    /// <summary>
    /// Writes a compiled skill to our assembly cache which is stored in an Azure File SHare.
    /// </summary>
    /// <param name="organizationIdentifier">Uniquely identifies the chat platform a skill belongs to.</param>
    /// <param name="skillCompilation">The compiled skill to write</param>
    public async Task WriteToCacheAsync(IOrganizationIdentifier organizationIdentifier, ISkillCompilation skillCompilation)
    {
        var cacheDirectory = await _assemblyCacheClient.GetOrCreateAssemblyCacheAsync(organizationIdentifier);
        var assemblyFile = cacheDirectory.GetAssemblyClient(skillCompilation.Name);
        if (await assemblyFile.ExistsAsync())
        {
            await assemblyFile.SetDateLastAccessedAsync(DateTimeOffset.UtcNow);

            // Assembly already exists. Don't need to overwrite it.
            return;
        }

        using var assemblyStream = new MemoryStream();
        using var symbolsStream = new MemoryStream();

        // Write to an interim memory stream so we can upload that stream.
        await skillCompilation.EmitAsync(assemblyStream, symbolsStream);
        if (assemblyStream.Length is 0)
        {
            throw new CompilationEmptyException($"Compilation {skillCompilation.Name} for {organizationIdentifier} is empty.");
        }
        assemblyStream.Position = 0;
        symbolsStream.Position = 0;
        await assemblyFile.UploadAsync(assemblyStream, symbolsStream);
    }

    async Task<IAssemblyClient> GetAssemblyClient(IOrganizationIdentifier organizationIdentifier, string cacheKey)
    {
        var cacheDirectory = await _assemblyCacheClient.GetOrCreateAssemblyCacheAsync(organizationIdentifier);
        return cacheDirectory.GetAssemblyClient(cacheKey);
    }
}
