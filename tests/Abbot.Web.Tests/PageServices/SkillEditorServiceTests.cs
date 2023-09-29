using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.PageServices;
using Serious.TestHelpers;
using Xunit;

public class SkillEditorServiceTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesAndCompilesCSharpSkillToAssemblyCache()
        {
            var env = TestEnvironment.Create();
            var (organization, user) = env.TestData;
            var updateModel = new SkillUpdateModel
            {
                Name = "beetlejuice",
                Code = "/* Valid code */"
            };

            var service = env.Activate<SkillEditorService>();

            var result = await service.CreateAsync(CodeLanguage.CSharp, updateModel, user, organization);
            Assert.Empty(result.CompilationErrors);
            Assert.NotNull(result.CompiledSkill);
            var skill = result.CompiledSkill;

            Assert.True(skill.Enabled);
            Assert.Equal("/* Valid code */", skill.Code);
            Assert.Equal("beetlejuice", skill.Name);
            string cacheKey = SkillCompiler.ComputeCacheKey(skill.Code);
            var compiled = env.AssemblyCache.CacheEntries(organization)[cacheKey];
            Assert.NotNull(compiled);
            var dbSkill = await env.Skills.GetAsync(skill.Name, skill.Organization);
            Assert.NotNull(dbSkill);
            Assert.Equal(cacheKey, dbSkill.CacheKey);
        }

        [Theory]
        [InlineData(CodeLanguage.Python, "# python")]
        [InlineData(CodeLanguage.JavaScript, "// js")]
        public async Task DoesNotCompilesNonCSharpSkill(CodeLanguage codeLanguage, string code)
        {
            var env = TestEnvironment.Create();
            var (organization, user) = env.TestData;
            var updateModel = new SkillUpdateModel
            {
                Name = "beetlejuice",
                Code = code
            };

            var service = env.Activate<SkillEditorService>();

            var result = await service.CreateAsync(codeLanguage, updateModel, user, organization);
            Assert.Empty(result.CompilationErrors);
            Assert.NotNull(result.CompiledSkill);
            var skill = result.CompiledSkill;

            Assert.True(skill.Enabled);
            Assert.Equal(code, skill.Code);
            Assert.Equal("beetlejuice", skill.Name);
            Assert.Empty(env.AssemblyCache.CacheEntries(organization));
        }

        [Fact]
        public async Task ReturnsCompilationErrorsIfAny()
        {
            const string code = "/* Valid code */";
            var env = TestEnvironment.Create();
            var (organization, user) = env.TestData;
            env.Compiler.AddCompilationResult(code,
          new FakeSkillCompilationResult(new FakeCompilationError()
          {
              ErrorId = "CS0001",
              Description = "You goofed it.",
          }));

            var updateModel = new SkillUpdateModel
            {
                Name = "beetlejuice",
                Code = code
            };

            var service = env.Activate<SkillEditorService>();

            var result = await service.CreateAsync(CodeLanguage.CSharp, updateModel, user, organization);
            Assert.Null(result.CompiledSkill);
            var err = Assert.Single(result.CompilationErrors);
            Assert.Equal("CS0001", err.ErrorId);
            Assert.Equal("You goofed it.", err.Description);
            Assert.False(env.AssemblyCache.CacheEntries(organization)
                .ContainsKey("c23fb84353a4a40c43a1ace6233e60939fae5056f49f5ab315a61e7d40bd4f77"));
        }
    }

    public class TheUpdateAsyncMethod
    {
        [Fact]
        public async Task UpdatesAndCompilesCSharpSkillToAssemblyCacheWhenCodeChanges()
        {
            var env = TestEnvironment.Create();
            var (organization, user) = env.TestData;
            var skill = await env.CreateSkillAsync("pulpfiction", codeText: "/* some code */");
            var updateModel = new SkillUpdateModel
            {
                Code = "/* Commented code */",
                UsageText = "No uses"
            };

            var service = env.Activate<SkillEditorService>();

            await service.UpdateAsync(skill, updateModel, user);

            Assert.True(skill.Enabled);
            Assert.Equal("/* Commented code */", skill.Code);
            Assert.Equal("No uses", skill.UsageText);
            var cacheKey = SkillCompiler.ComputeCacheKey(skill.Code);
            var compiled = env.AssemblyCache.CacheEntries(organization)[cacheKey];
            Assert.NotNull(compiled);
            var dbSkill = await env.Skills.GetAsync(skill.Name, skill.Organization);
            Assert.NotNull(dbSkill);
            Assert.Equal(cacheKey, dbSkill.CacheKey);
        }

        [Fact]
        public async Task UpdatesButDoesNotCompileCSharpSkillIfCodeDoesNotChange()
        {
            var env = TestEnvironment.Create();
            var (organization, user) = env.TestData;
            var skill = await env.CreateSkillAsync("pulpfiction", codeText: "/* some code */", cacheKey: "whatevs");
            var updateModel = new SkillUpdateModel
            {
                UsageText = "No uses"
            };

            var service = env.Activate<SkillEditorService>();

            await service.UpdateAsync(skill, updateModel, user);

            Assert.Equal("/* some code */", skill.Code);
            Assert.Equal("No uses", skill.UsageText);
            Assert.Empty(env.AssemblyCache.CacheEntries(organization));
        }

        [Theory]
        [InlineData(CodeLanguage.Python, "# python")]
        [InlineData(CodeLanguage.JavaScript, "// js")]
        public async Task UpdatesButDoesNotCompileNonCSharpSkillToAssemblyCacheWhenCodeChanges(
            CodeLanguage codeLanguage, string code)
        {
            var env = TestEnvironment.Create();
            var (organization, user) = env.TestData;
            var skill = await env.CreateSkillAsync(
                "pulpfiction",
                codeLanguage,
                codeText: "/* original */");

            var updateModel = new SkillUpdateModel
            {
                Code = code,
                UsageText = "No uses"
            };

            var service = env.Activate<SkillEditorService>();


            await service.UpdateAsync(skill, updateModel, user);

            Assert.True(skill.Enabled);
            Assert.Equal(code, skill.Code);
            Assert.Equal("No uses", skill.UsageText);
            Assert.Empty(env.AssemblyCache.CacheEntries(organization));
        }

        [Fact]
        public async Task DoesNotUpdateCacheAndReportsErrorsIfCompilationErrors()
        {
            const string code = "/* updated code */";
            var env = TestEnvironment.Create();
            var (organization, user) = env.TestData;
            var skill = await env.CreateSkillAsync("pulpfiction", codeText: "/* some code */");
            var updateModel = new SkillUpdateModel
            {
                Code = code,
                UsageText = "No uses"
            };
            env.Compiler.AddCompilationResult(code,
          new FakeSkillCompilationResult(new FakeCompilationError()
          {
              ErrorId = "CS0001",
              Description = "You goofed it.",
          }));

            var service = env.Activate<SkillEditorService>();

            var result = await service.UpdateAsync(skill, updateModel, user);
            Assert.False(result.Saved);
            var err = Assert.Single(result.CompilationErrors);
            Assert.Equal("CS0001", err.ErrorId);
            Assert.Equal("You goofed it.", err.Description);

            // Nothing was written to the cache
            Assert.Empty(env.AssemblyCache.CacheEntries(organization));
        }
    }
}
