using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Serious.Abbot.Functions.Cache;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class CompilationCacheTests
{
    public class TheGetCompiledSkillAsyncMethod
    {
        [Fact]
        public async Task DownloadsAssemblyForCacheMiss()
        {
            var skillApiClient = new FakeSkillApiClient(42);
            var compiledSkill = new FakeSkillAssembly();
            var skillIdentifier = new FakeSkillAssemblyIdentifier
            {
                PlatformId = "T001",
                PlatformType = PlatformType.Slack,
                SkillId = 42,
                CacheKey = "cacheKey"
            };
            skillApiClient.AddAssemblyDownload(skillIdentifier, compiledSkill);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new CompilationCache(memoryCache, skillApiClient);
            memoryCache.GetOrCreate("otherCacheKey", _ => new FakeSkillAssembly());

            var result = await cache.GetCompiledSkillAsync(skillIdentifier);

            Assert.Same(compiledSkill, result);
        }

        [Fact]
        public async Task DownloadsAssemblyForCacheMissWithRecompileRetryForBadFormatException()
        {
            var skillApiClient = new FakeSkillApiClient(42);
            var compiledSkill = new FakeSkillAssembly();
            var skillIdentifier = new FakeSkillAssemblyIdentifier
            {
                PlatformId = "T001",
                PlatformType = PlatformType.Slack,
                SkillId = 42,
                CacheKey = "cacheKey"
            };
            skillApiClient.AddBadFormatException(skillIdentifier, false);
            skillApiClient.AddAssemblyDownload(skillIdentifier, compiledSkill, true);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var cache = new CompilationCache(memoryCache, skillApiClient);

            var result = await cache.GetCompiledSkillAsync(skillIdentifier);

            Assert.Same(compiledSkill, result);
        }

        [Fact]
        public async Task RetrievesSkillFromMemoryCache()
        {
            var skillApiClient = new FakeSkillApiClient(46);
            var compiledSkill = new FakeSkillAssembly();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            memoryCache.GetOrCreate("cacheKey", _ => compiledSkill);
            var cache = new CompilationCache(memoryCache, skillApiClient);
            var skillIdentifier = new FakeSkillAssemblyIdentifier
            {
                SkillId = 46,
                CacheKey = "cacheKey"
            };

            var result = await cache.GetCompiledSkillAsync(skillIdentifier);

            Assert.Same(compiledSkill, result);
        }
    }
}
