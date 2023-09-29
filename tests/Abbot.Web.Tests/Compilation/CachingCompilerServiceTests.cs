using System.IO;
using System.Threading.Tasks;
using Serious.Abbot.Cache;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Compilation;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class CachingCompilerServiceTests
{
    public class TheExistsAsyncMethod
    {
        [Fact]
        public async Task ReturnsTrueIfAssemblyInCache()
        {
            const string code = "// some code";
            var cacheKey = SkillCompiler.ComputeCacheKey(code);
            var organizationIdentifier = new OrganizationIdentifier("T001", PlatformType.Slack);
            var compiler = new FakeSkillCompiler();
            var cache = new FakeAssemblyCache();
            var assemblyStream = new MemoryStream();
            await assemblyStream.WriteStringAsync("assembly");
            cache.AddAssemblyStream(organizationIdentifier, cacheKey, assemblyStream);
            var service = new CachingCompilerService(compiler, cache);

            var result = await service.ExistsAsync(organizationIdentifier, code);

            Assert.True(result);
        }

        [Fact]
        public async Task ReturnsFalseIfAssemblyNotInCache()
        {
            const string code = "// some code";
            var organizationIdentifier = new OrganizationIdentifier("T001", PlatformType.Slack);
            var compiler = new FakeSkillCompiler();
            var cache = new FakeAssemblyCache();
            var service = new CachingCompilerService(compiler, cache);

            var result = await service.ExistsAsync(organizationIdentifier, code);

            Assert.False(result);
        }
    }

    public class TheCompileAsyncMethod
    {
        [Fact]
        public async Task CompilesCodeAndUploadsToCache()
        {
            var compiler = new FakeSkillCompiler();
            var cache = new FakeAssemblyCache();
            var service = new CachingCompilerService(compiler, cache);
            var organizationIdentifier = new OrganizationIdentifier("T001", PlatformType.Slack);
            var compilation = new FakeSkillCompilation("// Code") { Name = "CacheKey" };
            var compilationResult = new FakeSkillCompilationResult(compilation);
            compiler.AddCompilationResult("// Code", compilationResult);

            var result = await service.CompileAsync(organizationIdentifier, CodeLanguage.CSharp, "// Code");

            Assert.Same(compilationResult, result);
            Assert.True(cache.WriteToCacheAsyncCalled);
            var cachedCompilation = cache.CacheEntries(organizationIdentifier)["CacheKey"];
            Assert.Same(compilation, cachedCompilation);
        }

        [Fact]
        public async Task CompilesCodeReturnsResultEvenIfUploadFails()
        {
            var compiler = new FakeSkillCompiler();
            var cache = new FakeAssemblyCache { ThrowOnUpload = true };
            var service = new CachingCompilerService(compiler, cache);
            var organizationIdentifier = new OrganizationIdentifier("T001", PlatformType.Slack);
            var compilation = new FakeSkillCompilation("// Code") { Name = "CacheKey" };
            var compilationResult = new FakeSkillCompilationResult(compilation);
            compiler.AddCompilationResult("// Code", compilationResult);

            var result = await service.CompileAsync(organizationIdentifier, CodeLanguage.CSharp, "// Code");

            Assert.Same(compilationResult, result);
            Assert.True(cache.WriteToCacheAsyncCalled);
            Assert.Empty(cache.CacheEntries(organizationIdentifier));
        }

        [Fact]
        public async Task CompilesCodeReturnsCompilationErrorIfOnlyComments()
        {
            var compiler = new FakeSkillCompiler();
            var cache = new FakeAssemblyCache { ThrowCompilationEmptyExceptionOnUpload = true };
            var service = new CachingCompilerService(compiler, cache);
            var organizationIdentifier = new OrganizationIdentifier("T001", PlatformType.Slack);
            var compilation = new FakeSkillCompilation("// Code") { Name = "CacheKey" };
            var compilationResult = new FakeSkillCompilationResult(compilation);
            compiler.AddCompilationResult("// Code", compilationResult);

            var result = await service.CompileAsync(organizationIdentifier, CodeLanguage.CSharp, "// Code");

            var compilationError = Assert.Single(result.CompilationErrors);
            Assert.Equal("Code consisting only of comments cannot be saved as a skill.", compilationError.Description);
        }
    }

    public class TheGetCachedAssemblyStreamAsyncMethod
    {
        [Fact]
        public async Task ReturnsAssemblyStreamForCachedRequest()
        {
            var organizationIdentifier = new OrganizationIdentifier("T001", PlatformType.Slack);
            var compiler = new FakeSkillCompiler();
            var cache = new FakeAssemblyCache();
            var assemblyStream = new MemoryStream();
            await assemblyStream.WriteStringAsync("assembly");
            cache.AddAssemblyStream(organizationIdentifier, "cacheKey", assemblyStream);
            cache.AddSymbolsStream(organizationIdentifier, "cacheKey", Stream.Null);
            var service = new CachingCompilerService(compiler, cache);
            var compilationRequest = new CompilationRequest(organizationIdentifier, "test", "cacheKey", CodeLanguage.CSharp)
            {
                Type = CompilationRequestType.Cached
            };

            var stream = await service.GetCachedAssemblyStreamAsync(compilationRequest);

            Assert.Equal(assemblyStream, stream);
        }

        [Fact]
        public async Task ReturnsSymbolsForSymbolsRequest()
        {
            var organizationIdentifier = new OrganizationIdentifier("T001", PlatformType.Slack);
            var compiler = new FakeSkillCompiler();
            var cache = new FakeAssemblyCache();
            var symbolsStream = new MemoryStream();
            await symbolsStream.WriteStringAsync("assembly");
            cache.AddAssemblyStream(organizationIdentifier, "cacheKey", Stream.Null);
            cache.AddSymbolsStream(organizationIdentifier, "cacheKey", symbolsStream);
            var service = new CachingCompilerService(compiler, cache);
            var compilationRequest = new CompilationRequest(organizationIdentifier, "test", "cacheKey", CodeLanguage.CSharp)
            {
                Type = CompilationRequestType.Symbols
            };

            var stream = await service.GetCachedAssemblyStreamAsync(compilationRequest);

            Assert.Equal(symbolsStream, stream);
        }

        [Fact]
        public async Task ReturnsNullStreamForRecompileRequest()
        {
            var organizationIdentifier = new OrganizationIdentifier("T001", PlatformType.Slack);
            var compiler = new FakeSkillCompiler();
            var cache = new FakeAssemblyCache();
            cache.AddAssemblyStream(organizationIdentifier, "cacheKey", new MemoryStream());
            cache.AddSymbolsStream(organizationIdentifier, "cacheKey", new MemoryStream());
            var service = new CachingCompilerService(compiler, cache);
            var compilationRequest = new CompilationRequest(organizationIdentifier, "test", "cacheKey", CodeLanguage.CSharp)
            {
                Type = CompilationRequestType.Recompile
            };

            var stream = await service.GetCachedAssemblyStreamAsync(compilationRequest);

            Assert.Equal(Stream.Null, stream);
        }
    }
}
