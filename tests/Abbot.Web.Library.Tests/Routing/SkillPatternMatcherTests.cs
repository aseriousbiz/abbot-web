using Serious.Abbot.Entities;
using Serious.Abbot.Routing;
using Serious.TestHelpers;

public class SkillPatternMatcherTests
{
    public class TheGetMatchingSkillAsyncMethod
    {
        [Fact]
        public async Task RetrievesAllPatternsThatMatchMessage()
        {
            var organization = new Organization { Id = 42, Slug = "abc42", PlatformId = "T01223" };
            var from = new Member { OrganizationId = organization.Id };
            var repository = new FakePatternRepository();
            var pattern1 = new SkillPattern
            {
                Name = "pattern-1",
                Slug = "slug-pattern-1",
                Pattern = ".*",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "basket-weaving", Organization = organization },
                Created = DateTime.UtcNow
            };
            var pattern2 = new SkillPattern
            {
                Name = "pattern-2",
                Slug = "slug-pattern-2",
                Pattern = "[a-z]+",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "whispering", Organization = organization },
                Created = DateTime.UtcNow.AddDays(-1),
                CaseSensitive = true
            };
            var pattern3 = new SkillPattern
            {
                Name = "pattern-3",
                Slug = "slug-pattern-3",
                Pattern = "[A-Z]+",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "yelling", Organization = organization },
                Created = DateTime.UtcNow.AddDays(-1),
                CaseSensitive = true
            };
            await repository.CreatePatternsAsync(pattern1, pattern2, pattern3);
            var patternMatcher = new SkillPatternMatcher(repository);
            var message = new FakePatternMatchableMessage("HELLO WORLD");

            var patterns = await patternMatcher.GetMatchingPatternsAsync(
                message,
                from,
                organization);

            Assert.Collection(patterns,
                p => Assert.Equal("pattern-3", p.Name),
                p => Assert.Equal("pattern-1", p.Name));
        }

        [Fact]
        public async Task DoesNotReturnPatternsForForeignMembers()
        {
            var organization = new Organization { Id = 42, Slug = "abc42", PlatformId = "T01223" };
            var from = new Member { OrganizationId = 23 };
            var repository = new FakePatternRepository();
            var pattern1 = new SkillPattern
            {
                Name = "pattern-1",
                Slug = "slug-pattern-1",
                Pattern = ".*",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "basket-weaving", Organization = organization },
                Created = DateTime.UtcNow
            };
            var pattern2 = new SkillPattern
            {
                Name = "pattern-2",
                Slug = "slug-pattern-2",
                Pattern = "[a-z]+",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "whispering", Organization = organization },
                Created = DateTime.UtcNow.AddDays(-1),
                CaseSensitive = true
            };
            var pattern3 = new SkillPattern
            {
                Name = "pattern-3",
                Slug = "slug-pattern-3",
                Pattern = "[A-Z]+",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "yelling", Organization = organization },
                Created = DateTime.UtcNow.AddDays(-1),
                CaseSensitive = true
            };
            await repository.CreatePatternsAsync(pattern1, pattern2, pattern3);
            var patternMatcher = new SkillPatternMatcher(repository);
            var message = new FakePatternMatchableMessage("HELLO WORLD");

            var patterns = await patternMatcher.GetMatchingPatternsAsync(
                message,
                from,
                organization);

            Assert.Empty(patterns);
        }

        [Fact]
        public async Task ReturnsExternallyCallablePatternsToForeignMembers()
        {
            var organization = new Organization { Id = 42, Slug = "abc42", PlatformId = "T01223" };
            var from = new Member { OrganizationId = 23 };
            var repository = new FakePatternRepository();
            var pattern1 = new SkillPattern
            {
                Name = "pattern-1",
                Slug = "slug-pattern-1",
                Pattern = ".*",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "basket-weaving", Organization = organization },
                Created = DateTime.UtcNow,
                AllowExternalCallers = true,
            };
            var pattern2 = new SkillPattern
            {
                Name = "pattern-2",
                Slug = "slug-pattern-2",
                Pattern = "[a-z]+",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "whispering", Organization = organization },
                Created = DateTime.UtcNow.AddDays(-1),
                CaseSensitive = true
            };
            var pattern3 = new SkillPattern
            {
                Name = "pattern-3",
                Slug = "slug-pattern-3",
                Pattern = "[A-Z]+",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "yelling", Organization = organization },
                Created = DateTime.UtcNow.AddDays(-1),
                CaseSensitive = true
            };
            await repository.CreatePatternsAsync(pattern1, pattern2, pattern3);
            var patternMatcher = new SkillPatternMatcher(repository);
            var message = new FakePatternMatchableMessage("HELLO WORLD");

            var patterns = await patternMatcher.GetMatchingPatternsAsync(
                message,
                from,
                organization);

            var matched = Assert.Single(patterns);
            Assert.Equal("pattern-1", matched.Name);
        }

        [Fact]
        public async Task OnlyIncludesFirstPatternThatMatchesForGivenSkill()
        {
            var organization = new Organization
            {
                Slug = "slack-42",
                PlatformId = "T01234",
                Id = 42
            };
            var from = new Member { OrganizationId = organization.Id };
            var repository = new FakePatternRepository();
            var skillWithMultiplePatterns = new Skill { Name = "basket-weaving", Organization = organization };
            var pattern1 = new SkillPattern
            {
                Name = "pattern-1",
                Slug = "slug-pattern-1",
                Pattern = ".*",
                PatternType = PatternType.RegularExpression,
                Skill = skillWithMultiplePatterns,
                Created = DateTime.Now
            };
            var pattern2 = new SkillPattern
            {
                Name = "pattern-2",
                Slug = "slug-pattern-2",
                Pattern = ".*",
                PatternType = PatternType.RegularExpression,
                Skill = skillWithMultiplePatterns,
                Created = DateTime.Now.AddDays(-2),
                CaseSensitive = false
            };
            var pattern3 = new SkillPattern
            {
                Name = "pattern-3",
                Slug = "slug-3",
                Pattern = ".*",
                PatternType = PatternType.RegularExpression,
                Skill = new Skill { Name = "yelling", Organization = organization },
                Created = DateTime.Now.AddDays(-1),
                CaseSensitive = false
            };
            await repository.CreatePatternsAsync(pattern1, pattern2, pattern3);
            var patternMatcher = new SkillPatternMatcher(repository);
            var message = new FakePatternMatchableMessage("HELLO WORLD");

            var patterns = await patternMatcher.GetMatchingPatternsAsync(
                message,
                from,
                organization);

            Assert.Collection(patterns,
                p => Assert.Equal("pattern-2", p.Name),
                p => Assert.Equal("pattern-3", p.Name));
        }

        [Fact]
        public async Task RetrievesNullWhenNoMatches()
        {
            var organization = new Organization { Id = 42, Slug = "abc42", PlatformId = "T01223" };
            var repository = new FakePatternRepository();
            var patternMatcher = new SkillPatternMatcher(repository);
            var from = new Member { OrganizationId = organization.Id };
            var message = new FakePatternMatchableMessage("hello world!");

            var pattern = await patternMatcher.GetMatchingPatternsAsync(
                message,
                from,
                organization);

            Assert.Empty(pattern);
        }
    }
}
