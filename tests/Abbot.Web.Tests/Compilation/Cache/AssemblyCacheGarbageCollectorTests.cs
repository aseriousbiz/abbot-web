using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Compilation;
using Serious.TestHelpers;
using Xunit;

public class AssemblyCacheGarbageCollectorTests
{
    public class TheRunAsyncMethod
    {
        [Fact]
        public async Task OnlyCollectsAssembliesThatHaveNotBeenAccessedForTwoHoursAndAreNotAssociatedWithASkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var secondOrganization = await env.CreateOrganizationAsync();

            const string bugCode = "// code bug";
            var bugCacheKey = SkillCompiler.ComputeCacheKey(bugCode);
            await env.CreateSkillAsync("bug", codeText: bugCode, cacheKey: bugCacheKey);

            const string rugCode = "// code rug";
            var rugCacheKey = SkillCompiler.ComputeCacheKey(rugCode);
            await env.CreateSkillAsync("rug", codeText: rugCode, cacheKey: rugCacheKey);

            const string tugCode = "// code tug";
            var tugCacheKey = SkillCompiler.ComputeCacheKey(tugCode);
            await env.CreateSkillAsync("tug", codeText: tugCode, cacheKey: tugCacheKey, org: secondOrganization);

            // DO NOT include 'tug' in the list of skills to be compiled.
            var cacheKeys = new[] { "PugCacheKey", bugCacheKey, rugCacheKey, "LugCachKey" };
            var directory = await env.AssemblyCacheClient.GetOrCreateAssemblyCacheAsync(organization);

            var assemblyClients = cacheKeys
                .Select(directory.GetAssemblyClient)
                .ToList();
            await Task.WhenAll(assemblyClients.Select(async c => {
                // Assembly can't be empty; symbols can
                var assemblyStream = new MemoryStream();
                await assemblyStream.WriteStringAsync("test");

                await c.UploadAsync(assemblyStream, new MemoryStream());
            }));
            Assert.All(await Task.WhenAll(assemblyClients.Select(c => c.ExistsAsync())), Assert.True);

            var garbageCollector = env.Activate<AssemblyCacheGarbageCollectionJob>();
            // Set last accessed date to 3 hours ago except for pug.
            await Task.WhenAll(assemblyClients
                .Skip(1)
                .Select(c => c.SetDateLastAccessedAsync(DateTimeOffset.UtcNow.AddHours(-3))));

            await garbageCollector.RunAsync(default);

            Assert.True(await assemblyClients[0].ExistsAsync());
            Assert.True(await assemblyClients[1].ExistsAsync());
            Assert.True(await assemblyClients[2].ExistsAsync());
            Assert.False(await assemblyClients[3].ExistsAsync()); // Not associated with skill and stale

            // We shouldn't create caches if they don't already exist.
            Assert.Null(await env.AssemblyCacheClient.GetAssemblyCacheAsync(env.TestData.ForeignOrganization));
            Assert.Null(await env.AssemblyCacheClient.GetAssemblyCacheAsync(secondOrganization));
        }
    }
}
