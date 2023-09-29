using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Compilation;
using Serious.Abbot.Controllers;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class CompilationControllerTests
{
    public class TheGetAssemblyAsyncMethod : ControllerTestBase<CompilationController>
    {
        [Fact]
        public async Task ReturnsNotFoundForNonExistentSkill()
        {
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = "CacheKey",
                PlatformId = "T001",
                PlatformType = PlatformType.Slack,
                Type = CompilationRequestType.Cached
            };

            AuthenticateAs(Env.TestData.Member, new(404));

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DownloadsAssemblyFromFileShareCache()
        {
            var organization = Env.TestData.Organization;
            var skill = await Env.CreateSkillAsync("test");
            var sourceBytes = new byte[] { 1, 2, 3 };
            var assemblyStream = new MemoryStream(sourceBytes) { Position = 0 };
            Env.CachingCompilerService.AddAssemblyStream(skill.Organization, skill.CacheKey, assemblyStream);
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = skill.CacheKey,
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Cached
            };

            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));

            var fsResult = Assert.IsType<FileStreamResult>(result);
            var buffer = new byte[3];
            await fsResult.FileStream.ReadAsync(buffer, 0, 3);
            Assert.Equal(sourceBytes, buffer);
        }

        [Fact]
        public async Task DownloadsAssemblyFromFileShareCacheForUnsavedChanges()
        {
            var organization = Env.TestData.Organization;
            var skill = await Env.CreateSkillAsync("test");
            var sourceBytes = new byte[] { 1, 2, 3 };
            var assemblyStream = new MemoryStream(sourceBytes) { Position = 0 };
            Env.CachingCompilerService.AddAssemblyStream(skill.Organization, "new cache key", assemblyStream);
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = "new cache key",
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Cached
            };

            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));

            var fsResult = Assert.IsType<FileStreamResult>(result);
            var buffer = new byte[3];
            await fsResult.FileStream.ReadAsync(buffer, 0, 3);
            Assert.Equal(sourceBytes, buffer);
        }

        [Fact]
        public async Task DownloadsAssemblyFromFileShareCacheForUnsavedSkill()
        {
            var organization = Env.TestData.Organization;
            var sourceBytes = new byte[] { 1, 2, 3 };
            var assemblyStream = new MemoryStream(sourceBytes) { Position = 0 };
            Env.CachingCompilerService.AddAssemblyStream(organization, "TheCacheKey", assemblyStream);
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = "TheCacheKey",
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Cached
            };

            var skill = await Env.CreateSkillAsync("test");
            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));

            var fsResult = Assert.IsType<FileStreamResult>(result);
            var buffer = new byte[3];
            await fsResult.FileStream.ReadAsync(buffer, 0, 3);
            Assert.Equal(sourceBytes, buffer);
        }

        [Fact]
        public async Task ThrowsExceptionIfUnsavedChangesAreNotInCache()
        {
            var organization = Env.TestData.Organization;
            var skill = await Env.CreateSkillAsync("test");
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = "new cache key",
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Cached
            };

            AuthenticateAs(Env.TestData.Member, skill);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest)));
        }

        [Fact]
        public async Task CompilesAssemblyIfNotInCacheAndReturnsCompilation()
        {
            var organization = Env.TestData.Organization;
            var skill = await Env.CreateSkillAsync("test", codeText: "await Bot.ReplyAsync(\"hello!\");");
            var compiledSkill = new FakeSkillCompilation("AssemblyContents", "SymbolsContents");
            Env.CachingCompilerService.AddCompilationResult(
                organization,
                "await Bot.ReplyAsync(\"hello!\");",
                new FakeSkillCompilationResult(compiledSkill));
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = skill.CacheKey,
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Cached
            };
            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));

            var fsResult = Assert.IsType<FileStreamResult>(result);
            var resultAssemblyContent = await fsResult.FileStream.ReadAsStringAsync();
            Assert.Equal("AssemblyContents", resultAssemblyContent);
        }

        [Fact]
        public async Task ReturnsCompilationErrors()
        {
            var organization = Env.TestData.Organization;
            var skill = await Env.CreateSkillAsync("test");
            var compilationResult = new FakeSkillCompilationResult(new List<ICompilationError>
            {
                new FakeCompilationError { ErrorId = "123", Description = "Bad things happened." }
            });
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = skill.CacheKey,
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Cached
            };
            Env.CachingCompilerService.AddCompilationResult(compilationRequest, skill.Code, compilationResult);

            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            var errors = Assert.IsAssignableFrom<IImmutableList<ICompilationError>>(objectResult.Value);
            Assert.NotNull(errors);
            var error = Assert.Single(errors);
            Assert.NotNull(error);
            Assert.Equal("Bad things happened.", error.Description);
        }

        [Fact]
        public async Task CompilesAssemblyIfRecompileRequestedEvenIfInCache()
        {
            var organization = Env.TestData.Organization;
            var skill = await Env.CreateSkillAsync("test");
            var sourceBytes = new byte[] { 1, 2, 3 };
            var assemblyStream = new MemoryStream(sourceBytes) { Position = 0 };
            Env.CachingCompilerService.AddAssemblyStream(skill.Organization, skill.CacheKey, assemblyStream);
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = skill.CacheKey,
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Recompile

            };
            Env.CachingCompilerService.AddCompilationResult(compilationRequest, skill.Code, new FakeSkillCompilationResult("// new code"));

            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));

            Assert.IsType<FileStreamResult>(result);
        }

        [Fact]
        public async Task DownloadsSymbolsFromFileShareCache()
        {
            var organization = Env.TestData.Organization;
            var skill = await Env.CreateSkillAsync("test");
            var symbolsStream = new MemoryStream();
            await symbolsStream.WriteStringAsync("Symbols Contents");
            Env.CachingCompilerService.AddSymbolsStream(skill.Organization, skill.CacheKey, symbolsStream);
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = skill.CacheKey,
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Symbols
            };

            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));

            var fsResult = Assert.IsType<FileStreamResult>(result);
            var contents = await fsResult.FileStream.ReadAsStringAsync();
            Assert.Equal("Symbols Contents", contents);
        }

        [Fact]
        public async Task ReturnsEmptySymbolsIfSymbolsNotInCache()
        {
            var organization = Env.TestData.Organization;
            var skill = await Env.CreateSkillAsync("test");
            var compilationRequest = new CompilationRequest
            {
                SkillName = "test",
                CacheKey = skill.CacheKey,
                PlatformId = organization.PlatformId,
                PlatformType = organization.PlatformType,
                Type = CompilationRequestType.Symbols
            };

            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAssemblyAsync(compilationRequest));

            var fsResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal(0, fsResult.FileStream.Length);
        }
    }
}
