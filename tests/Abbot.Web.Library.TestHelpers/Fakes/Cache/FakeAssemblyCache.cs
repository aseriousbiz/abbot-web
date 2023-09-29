using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Serious.Abbot.Compilation;
using Serious.Abbot.Messages;

namespace Serious.TestHelpers
{
    public class FakeAssemblyCache : IAssemblyCache
    {
        readonly Dictionary<string, Dictionary<string, ISkillCompilation>> _cached = new();
        readonly Dictionary<string, Stream> _assemblyStreams = new();
        readonly Dictionary<string, Stream> _symbolsStreams = new();

        public bool ThrowOnUpload { get; set; }

        public bool ThrowCompilationEmptyExceptionOnUpload { get; set; }

        public bool WriteToCacheAsyncCalled { get; private set; }

        public Task<bool> AssemblyExistsAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey)
        {
            return Task.FromResult(_assemblyStreams.ContainsKey(organizationIdentifier.ToCacheKey(cacheKey)));
        }

        public Task<Stream> DownloadAssemblyAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey)
        {
            return Task.FromResult(
                _assemblyStreams.TryGetValue(organizationIdentifier.ToCacheKey(cacheKey), out var stream)
                    ? stream
                    : Stream.Null);
        }

        public Task<Stream> DownloadSymbolsAsync(IOrganizationIdentifier organizationIdentifier, string cacheKey)
        {
            return Task.FromResult(
                _symbolsStreams.TryGetValue(organizationIdentifier.ToCacheKey(cacheKey), out var stream)
                    ? stream
                    : Stream.Null);
        }

        public Task WriteToCacheAsync(IOrganizationIdentifier organizationIdentifier, ISkillCompilation skillCompilation)
        {
            WriteToCacheAsyncCalled = true;
            if (ThrowCompilationEmptyExceptionOnUpload)
            {
                throw new CompilationEmptyException();
            }
            if (ThrowOnUpload)
            {
                throw new InvalidOperationException("Upload failed");
            }

            var cache = _cached.GetOrCreate(
                organizationIdentifier.ToCacheKey(),
                _ => new Dictionary<string, ISkillCompilation>());
            cache[skillCompilation.Name] = skillCompilation;
            return Task.CompletedTask;
        }

        public IReadOnlyDictionary<string, ISkillCompilation> CacheEntries(IOrganizationIdentifier organizationIdentifier) => _cached.TryGetValue(organizationIdentifier.ToCacheKey(), out var cache)
            ? cache
            : new Dictionary<string, ISkillCompilation>();

        public IReadOnlyDictionary<string, Stream> CachedAssemblyStreams => _assemblyStreams;
        public IReadOnlyDictionary<string, Stream> CachedSymbolsStreams => _symbolsStreams;

        public void AddAssemblyStream(IOrganizationIdentifier organizationIdentifier, string cacheKey, Stream assemblyStream)
        {
            _assemblyStreams.Add(organizationIdentifier.ToCacheKey(cacheKey), assemblyStream);
        }

        public void AddSymbolsStream(IOrganizationIdentifier organizationIdentifier, string cacheKey, Stream symbolsStream)
        {
            _symbolsStreams.Add(organizationIdentifier.ToCacheKey(cacheKey), symbolsStream);
        }
    }
}
