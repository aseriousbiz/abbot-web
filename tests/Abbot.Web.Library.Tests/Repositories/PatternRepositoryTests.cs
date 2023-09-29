using System.Collections.Generic;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Xunit;

public class PatternRepositoryTests
{
    public class TheGetAllAsyncMethod
    {
        [Fact]
        public async Task RetrievesPatternsForEnabledNotDeletedSkills()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var skill1 = await env.CreateSkillAsync("test-skill1");
            skill1.Patterns = new List<SkillPattern>
            {
                new()
                {
                    Name = "pattern 1",
                    Slug = "slug-pattern-1",
                    Pattern = "slug-pattern-1"
                },
                new()
                {
                    Name = "pattern 2",
                    Slug = "slug-pattern-2",
                    Pattern = "slug-pattern-2"
                }
            };
            var skill2 = await env.CreateSkillAsync("test-skill2", enabled: false);
            skill2.Patterns = new List<SkillPattern>
            {
                new()
                {
                    Name = "pattern 3",
                    Slug = "slug-pattern-3",
                    Pattern = "slug-pattern-3"
                },
            };
            var skill3 = await env.CreateSkillAsync("test-skill3");
            skill3.IsDeleted = true;
            skill3.Patterns = new List<SkillPattern>
            {
                new()
                {
                    Name = "pattern 4",
                    Slug = "slug",
                    Pattern = "foo-bar"
                }
            };
            var skill4 = await env.CreateSkillAsync("test-skill4", org: env.TestData.ForeignOrganization);
            skill4.Patterns = new List<SkillPattern>
            {
                new()
                {
                    Pattern = "plaid",
                    Slug = "plaid-pattern",
                    Name = "pattern 5"
                }

            };
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<PatternRepository>();

            var patterns = await repository.GetAllAsync(organization);

            Assert.Equal(2, patterns.Count);
            Assert.Equal("test-skill1", patterns[0].Skill.Name);
            Assert.Equal("test-skill1", patterns[1].Skill.Name);
        }
    }

    public class TheGetAsyncMethod
    {
        [Fact]
        public async Task GetsPatternBySlug()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var organization = env.TestData.Organization;
            var skill = await env.CreateSkillAsync("pattern-responder");
            var repository = env.Activate<PatternRepository>();
            await repository.CreateAsync(
                "A really cool pattern",
                "inside",
                PatternType.Contains,
                true,
                skill,
                user,
                true);

            var retrieved = await repository.GetAsync(
                skill.Name,
                "a-really-cool-pattern",
                organization);

            Assert.NotNull(retrieved);
            Assert.Equal("a-really-cool-pattern", retrieved.Slug);
            Assert.Equal("A really cool pattern", retrieved.Name);
            Assert.Equal("inside", retrieved.Pattern);
            Assert.Equal(PatternType.Contains, retrieved.PatternType);
            Assert.True(retrieved.CaseSensitive);
        }
    }

    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesPatternWithSlug()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("pattern-responder");
            var repository = env.Activate<PatternRepository>();
            var pattern = await repository.CreateAsync(
                name: "A really cool pattern",
                pattern: "inside",
                PatternType.Contains,
                caseSensitive: true,
                skill,
                user,
                enabled: true);

            // Re-retrieve the pattern to make sure it's saved properly
            pattern = await repository.GetAsync(skill.Name, pattern.Slug, skill.Organization);

            Assert.NotNull(pattern);
            Assert.Equal("a-really-cool-pattern", pattern.Slug);
            Assert.Equal("A really cool pattern", pattern.Name);
            Assert.Equal("inside", pattern.Pattern);
            Assert.Equal(PatternType.Contains, pattern.PatternType);
            Assert.True(pattern.CaseSensitive);
            Assert.True(pattern.Enabled);
            Assert.False(pattern.AllowExternalCallers);
        }

        [Fact]
        public async Task CreatesExternallyCallablePatternWithSlug()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("pattern-test");
            var repository = env.Activate<PatternRepository>();
            var pattern = await repository.CreateAsync(
                name: "A really great pattern",
                pattern: "outside",
                PatternType.StartsWith,
                caseSensitive: false,
                skill,
                user,
                enabled: false,
                allowExternalCallers: true);

            // Re-retrieve the pattern to make sure it's saved properly
            pattern = await repository.GetAsync(skill.Name, pattern.Slug, skill.Organization);

            Assert.NotNull(pattern);
            Assert.Equal("a-really-great-pattern", pattern.Slug);
            Assert.Equal("A really great pattern", pattern.Name);
            Assert.Equal("outside", pattern.Pattern);
            Assert.Equal(PatternType.StartsWith, pattern.PatternType);
            Assert.False(pattern.CaseSensitive);
            Assert.False(pattern.Enabled);
            Assert.True(pattern.AllowExternalCallers);
        }
    }

    public class TheUpdateAsyncMethod
    {
        [Fact]
        public async Task UpdatesSlug()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("pattern-responder");
            var repository = env.Activate<PatternRepository>();
            var pattern = await repository.CreateAsync(
                name: "A really cool pattern",
                pattern: "inside",
                PatternType.Contains,
                caseSensitive: true,
                skill,
                user,
                true);
            pattern.Name = "an ok pattern";

            await repository.UpdateAsync(pattern, user);

            Assert.Equal("an-ok-pattern", pattern.Slug);
            Assert.Equal("an ok pattern", pattern.Name);
            Assert.Equal("inside", pattern.Pattern);
            Assert.Equal(PatternType.Contains, pattern.PatternType);
            Assert.True(pattern.CaseSensitive);
        }
    }
}
