using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Execution;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Logging;

namespace Serious.Abbot.Functions.Cache;

/// <summary>
/// Read compiled skill from the memory cache and then the Azure File Storage cache.
/// </summary>
public class CompilationCache : ICompilationCache
{
    static readonly ILogger<CompilationCache> Log = ApplicationLoggerFactory.CreateLogger<CompilationCache>();

    readonly IMemoryCache _memoryCache;
    readonly ISkillApiClient _apiClient;

    /// <summary>
    /// Constructs a <see cref="CompilationCache"/>.
    /// </summary>
    /// <param name="memoryCache">The <see cref="IMemoryCache"/> to store the compilation in.</param>
    /// <param name="apiClient">The <see cref="ISkillApiClient"/> used to download the compilation.</param>
    public CompilationCache(IMemoryCache memoryCache, ISkillApiClient apiClient)
    {
        _memoryCache = memoryCache;
        _apiClient = apiClient;
    }

    /// <summary>
    /// Retrieves a compiled skill based on the skill id and the cache key.
    /// </summary>
    /// <remarks>Attempts to retrieve the skill from the cache first. If we get a
    /// <see cref="BadImageFormatException"/> we request a recompiled version.</remarks>
    /// <param name="skillAssemblyIdentifier">Uniquely identifies a skill assembly</param>
    /// <returns>The compiled skill from the cache or null if it's not in the cache.</returns>
    public async Task<ICompiledSkill> GetCompiledSkillAsync(ICompiledSkillIdentifier skillAssemblyIdentifier)
    {
        try
        {
            return await GetCompiledSkillAsync(
                skillAssemblyIdentifier,
                recompile: false);
        }
        catch (BadImageFormatException)
        {
            return await GetCompiledSkillAsync(
                skillAssemblyIdentifier,
                recompile: true);
        }
    }

    async Task<ICompiledSkill> GetCompiledSkillAsync(
        ICompiledSkillIdentifier skillAssemblyIdentifier,
        bool recompile)
    {
        if (recompile)
        {
            return await _apiClient.DownloadCompiledSkillAsync(skillAssemblyIdentifier, recompile);
        }

        return await _memoryCache.GetOrCreateAsync(
            skillAssemblyIdentifier.CacheKey,
            async cacheEntry => {
                Log.AssemblyNotFoundInCache(
                    skillAssemblyIdentifier.SkillId,
                    skillAssemblyIdentifier.SkillName,
                    skillAssemblyIdentifier.CacheKey,
                    skillAssemblyIdentifier.PlatformId);
                cacheEntry.Size = 1;
                cacheEntry.SlidingExpiration = TimeSpan.FromDays(7);
                return await _apiClient.DownloadCompiledSkillAsync(skillAssemblyIdentifier, recompile: false);
            });
    }
}
