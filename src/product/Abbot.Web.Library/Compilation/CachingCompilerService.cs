using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.Compilation;

/// <summary>
/// Compiles a skill and caches it
/// </summary>
public class CachingCompilerService : ICachingCompilerService
{
    static readonly ILogger<CachingCompilerService> Log = ApplicationLoggerFactory.CreateLogger<CachingCompilerService>();

    readonly ISkillCompiler _skillCompiler;
    readonly IAssemblyCache _assemblyCache;

    public CachingCompilerService(ISkillCompiler skillCompiler, IAssemblyCache assemblyCache)
    {
        _skillCompiler = skillCompiler;
        _assemblyCache = assemblyCache;
    }

    public Task<bool> ExistsAsync(IOrganizationIdentifier organizationIdentifier, string code)
    {
        return _assemblyCache.AssemblyExistsAsync(organizationIdentifier, SkillCompiler.ComputeCacheKey(code));
    }

    public async Task<ICompilationResult> CompileAsync(
        IOrganizationIdentifier organizationIdentifier,
        CodeLanguage language,
        string code)
    {
        var compilation = await _skillCompiler.CompileAsync(language, code);
        if (!compilation.CompilationErrors.Any())
        {
            try
            {
                await _assemblyCache.WriteToCacheAsync(organizationIdentifier, compilation.CompiledSkill);
            }
            catch (CompilationEmptyException)
            {
                var errors = new[] { new EmptyCompilationError() }
                    .Cast<ICompilationError>()
                    .ToImmutableList();
                return new SkillCompilationResult(compilation.CompiledSkill, errors);
            }
            catch (Exception ex)
            {
                Log.ExceptionUploadingSkillAssembly(ex, compilation.CompiledSkill.Name, organizationIdentifier.PlatformId, organizationIdentifier.PlatformType);
            }
        }

        return compilation;
    }

    public async Task<Stream> GetCachedAssemblyStreamAsync(CompilationRequest compilationRequest)
    {
        return compilationRequest.Type switch
        {
            CompilationRequestType.Symbols => await _assemblyCache.DownloadSymbolsAsync(compilationRequest, compilationRequest.CacheKey),
            CompilationRequestType.Recompile => Stream.Null,
            _ => await _assemblyCache.DownloadAssemblyAsync(compilationRequest, compilationRequest.CacheKey)
        };
    }
}
