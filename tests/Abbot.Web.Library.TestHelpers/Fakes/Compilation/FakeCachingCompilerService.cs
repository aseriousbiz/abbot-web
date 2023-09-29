using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Compilation;
using Serious.Abbot.Messages;

namespace Serious.TestHelpers
{
    public class FakeCachingCompilerService : ICachingCompilerService
    {
        readonly Dictionary<string, ICompilationResult> _compilationResults = new();
        readonly Dictionary<string, Stream> _assemblyStreams = new();
        readonly Dictionary<string, Stream> _symbolsStreams = new();

        public Task<bool> ExistsAsync(IOrganizationIdentifier organizationIdentifier, string code)
        {
            return Task.FromResult(
                OverrideExistAsyncToReturnTrue
                || _assemblyStreams.ContainsKey(GetCacheKey(organizationIdentifier, SkillCompiler.ComputeCacheKey(code))));
        }

        public bool OverrideExistAsyncToReturnTrue { get; set; }

        public bool CompileAsyncCalled { get; set; }

        public Task<ICompilationResult> CompileAsync(IOrganizationIdentifier organizationIdentifier,
            CodeLanguage language,
            string code)
        {
            CompileAsyncCalled = true;
            return Task.FromResult(_compilationResults[GetCompilationResultCacheKey(organizationIdentifier, code)]);
        }

        public void AddCompilationResult(IOrganizationIdentifier organizationIdentifier, string code,
            ICompilationResult compilationResult)
        {
            _compilationResults.Add(GetCompilationResultCacheKey(organizationIdentifier, code), compilationResult);
        }

        public Task<Stream> GetCachedAssemblyStreamAsync(CompilationRequest compilationRequest)
        {
            var stream = compilationRequest.Type switch
            {
                CompilationRequestType.Symbols => GetStream(_symbolsStreams, compilationRequest, compilationRequest.CacheKey),
                CompilationRequestType.Recompile => Stream.Null,
                _ => GetStream(_assemblyStreams, compilationRequest, compilationRequest.CacheKey)
            };

            return Task.FromResult(stream);
        }

        static Stream GetStream(IReadOnlyDictionary<string, Stream> streams, CompilationRequest compilationRequest, string cacheKey)
        {
            return streams.TryGetValue(GetCacheKey(compilationRequest, cacheKey), out var stream)
                ? stream
                : Stream.Null;
        }

        public void AddAssemblyStream(IOrganizationIdentifier organizationIdentifier, string cacheKey, Stream assemblyStream)
        {
            _assemblyStreams.Add(GetCacheKey(organizationIdentifier, cacheKey), assemblyStream);
        }

        public void AddSymbolsStream(IOrganizationIdentifier organizationIdentifier, string cacheKey, Stream symbolsStream)
        {
            _symbolsStreams.Add(GetCacheKey(organizationIdentifier, cacheKey), symbolsStream);
        }

        static string GetCompilationResultCacheKey(
            IOrganizationIdentifier organizationIdentifier,
            string code)
        {
            return $"{organizationIdentifier.PlatformType}:{organizationIdentifier.PlatformId}:{code}";
        }

        static string GetCacheKey(IOrganizationIdentifier organizationIdentifier, string cacheKey)
        {
            return $"{organizationIdentifier.PlatformType}:{organizationIdentifier.PlatformId}:{cacheKey}";
        }
    }
}
