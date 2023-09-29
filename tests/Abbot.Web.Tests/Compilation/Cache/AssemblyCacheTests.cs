using System;
using System.IO;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Serious.Abbot.Cache;
using Serious.Abbot.Compilation;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class AssemblyCacheTests
{
    public class TheAssemblyExistsAsyncMethod
    {
        [Fact]
        public async Task ReturnsTrueIfAssemblyIsCached()
        {
            var organizationIdentifier = new OrganizationIdentifier("U001", PlatformType.Slack);
            var assemblyCacheClient = new FakeAssemblyCacheClient();
            var assemblyCacheDirectoryClient = await assemblyCacheClient.GetOrCreateAssemblyCacheAsync(organizationIdentifier);
            var assemblyClient = assemblyCacheDirectoryClient.GetAssemblyClient("SomeCacheKey") as FakeAssemblyClient;
            var (assemblyStream, _) = await GetAssemblyStreams("return \"yay! It worked!\";");
            Assert.True(assemblyStream.Length > 0);
            assemblyStream.Position = 0;
            Assert.NotNull(assemblyClient);
            assemblyClient.FakeAssemblyFileClient.Exists = true;
            assemblyClient.FakeAssemblyFileClient.Content = assemblyStream;
            var assemblyCache = new AssemblyCache(assemblyCacheClient);

            var result = await assemblyCache.AssemblyExistsAsync(organizationIdentifier, "SomeCacheKey");

            Assert.True(result);
        }

        [Fact]
        public async Task ReturnsTrueIfAssemblyIsNotCached()
        {
            var organizationIdentifier = new OrganizationIdentifier("U001", PlatformType.Slack);
            var assemblyCacheClient = new FakeAssemblyCacheClient();
            var assemblyCacheDirectoryClient = await assemblyCacheClient.GetOrCreateAssemblyCacheAsync(organizationIdentifier);
            var assemblyClient = assemblyCacheDirectoryClient.GetAssemblyClient("SomeCacheKey") as FakeAssemblyClient;
            assemblyClient!.FakeAssemblyFileClient.Exists = false;
            var assemblyCache = new AssemblyCache(assemblyCacheClient);

            var result = await assemblyCache.AssemblyExistsAsync(organizationIdentifier, "SomeCacheKey");

            Assert.False(result);
        }
    }

    public class TheWriteToCacheAsyncMethod
    {
        [Fact]
        public async Task WritesCompilationToAssemblyClientAndSetsDateLastAccessed()
        {
            var now = DateTimeOffset.UtcNow;
            var cacheClient = new FakeAssemblyCacheClient();
            var cache = new AssemblyCache(cacheClient);
            var organizationIdentifier = new OrganizationIdentifier("U001", PlatformType.Slack);
            var skillCompilation = new FakeSkillCompilation("AssemblyCompilation", "AssemblySymbols")
            {
                Name = "somehash"
            };

            await cache.WriteToCacheAsync(organizationIdentifier, skillCompilation);

            var cacheDirectory = await cacheClient.GetOrCreateAssemblyCacheAsync(organizationIdentifier);
            var assemblyFile = cacheDirectory.GetAssemblyClient("somehash") as FakeAssemblyClient;
            Assert.NotNull(assemblyFile);
            Assert.True(await assemblyFile.ExistsAsync());
            assemblyFile.FakeAssemblyFileClient.Content!.Position = 0;
            using var assemblyReader = new StreamReader(assemblyFile.FakeAssemblyFileClient.Content);
            var assemblyStandIn = await assemblyReader.ReadToEndAsync();
            Assert.Equal("AssemblyCompilation", assemblyStandIn);
            assemblyFile.FakeAssemblySymbolsFileClient.Content!.Position = 0;
            using var symbolsReader = new StreamReader(assemblyFile.FakeAssemblySymbolsFileClient.Content);
            var symbolsStandIn = await symbolsReader.ReadToEndAsync();
            Assert.Equal("AssemblySymbols", symbolsStandIn);
            Assert.True(await assemblyFile.GetDateLastAccessedAsync() > now);
        }

        [Fact]
        public async Task ThrowsExceptionWhenCompilationIsEmpty()
        {
            // This test simulates a compilation where the code is all comments.

            var cacheClient = new FakeAssemblyCacheClient();
            var cache = new AssemblyCache(cacheClient);
            var organizationIdentifier = new OrganizationIdentifier("U001", PlatformType.Slack);
            var skillCompilation = new FakeSkillCompilation("", "")
            {
                Name = "somehash"
            };


            await Assert.ThrowsAsync<CompilationEmptyException>(
                () => cache.WriteToCacheAsync(organizationIdentifier, skillCompilation));
        }
    }

    public class TheDownloadAssemblyAsyncMethod
    {
        [Fact]
        public async Task DownloadsUsableAssemblyAndSetsAccessDate()
        {
            var now = DateTimeOffset.UtcNow;
            var organizationIdentifier = new OrganizationIdentifier("U001", PlatformType.Slack);
            var assemblyCacheClient = new FakeAssemblyCacheClient();
            var assemblyCacheDirectoryClient = await assemblyCacheClient.GetOrCreateAssemblyCacheAsync(organizationIdentifier);
            var assemblyClient = assemblyCacheDirectoryClient.GetAssemblyClient("SomeCacheKey") as FakeAssemblyClient;
            var (assemblyStream, _) = await GetAssemblyStreams("return \"yay! It worked!\";");
            Assert.True(assemblyStream.Length > 0);
            assemblyStream.Position = 0;
            Assert.NotNull(assemblyClient);
            assemblyClient.FakeAssemblyFileClient.Exists = true;
            assemblyClient.FakeAssemblyFileClient.Content = assemblyStream;
            var assemblyCache = new AssemblyCache(assemblyCacheClient);

            var stream = await assemblyCache.DownloadAssemblyAsync(organizationIdentifier, "SomeCacheKey");

            Assert.True(stream.Length > 0);
            var result = await RunAssembly(stream);
            Assert.Equal("yay! It worked!", result);
            Assert.True(await assemblyClient.GetDateLastAccessedAsync() >= now);
        }
    }

    public class TheDownloadSymbolsAsyncMethod
    {
        [Fact]
        public async Task DownloadsSymbolsAssembly()
        {
            var organizationIdentifier = new OrganizationIdentifier("U001", PlatformType.Slack);
            var assemblyCacheClient = new FakeAssemblyCacheClient();
            var assemblyCacheDirectoryClient = await assemblyCacheClient.GetOrCreateAssemblyCacheAsync(organizationIdentifier)
                as FakeAssemblyCacheDirectoryClient;
            Assert.NotNull(assemblyCacheDirectoryClient);
            var assemblyClient = assemblyCacheDirectoryClient.GetAssemblyClient("SomeCacheKey")
                as FakeAssemblyClient;
            var (_, symbolsStream) = await GetAssemblyStreams("return \"yay! It worked!\";");
            Assert.True(symbolsStream.Length > 0);
            symbolsStream.Position = 0;
            Assert.NotNull(assemblyClient);
            assemblyClient.FakeAssemblySymbolsFileClient.Exists = true;
            assemblyClient.FakeAssemblySymbolsFileClient.Content = symbolsStream;
            var assemblyCache = new AssemblyCache(assemblyCacheClient);

            var stream = await assemblyCache.DownloadSymbolsAsync(organizationIdentifier, "SomeCacheKey");

            Assert.True(stream.Length > 0);
        }
    }

    static async Task<(Stream, Stream)> GetAssemblyStreams(string code)
    {
        var options = ScriptOptions.Default
            .WithImports("System")
            .WithEmitDebugInformation(true);
        var script = new DotNetScript(CSharpScript.Create<string>(code, options, typeof(object)));
        Assert.Empty(script.Compile());
        var compilation = new SkillCompilation("nomdeplume", script);
        var assemblyStream = new MemoryStream();
        var symbolsStream = new MemoryStream();

        await compilation.EmitAsync(assemblyStream, symbolsStream);

        return (assemblyStream, symbolsStream);
    }

    static async Task<string> RunAssembly(Stream assemblyStream)
    {
        assemblyStream.Position = 0;
        var assemblyLoadContext = new ScriptHelper.CollectibleAssemblyLoadContext();
        var assembly = assemblyLoadContext.LoadFromStream(assemblyStream);
        var type = assembly.GetType("Submission#0");
        Assert.NotNull(type);
        var method = type.GetMethod("<Factory>");
        Assert.NotNull(method);
        var parameters = new object[] { new[] { new object(), null } };
        return await (method.Invoke(null, parameters) as Task<string>)!;
    }
}
