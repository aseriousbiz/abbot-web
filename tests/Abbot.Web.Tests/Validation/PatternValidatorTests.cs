using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;
using Serious.Abbot.Validation;
using Xunit;

public class PatternValidatorTests
{
    public class TheIsUniqueNameMethod
    {
        [Fact]
        public async Task ReturnsTrueWhenSkillHasNoOtherPatterns()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("butterfly");
            var validator = env.Activate<PatternValidator>();

            var result = await validator.IsUniqueNameAsync(
                "cocoon",
                0,
                "butterfly pattern",
                skill.Organization);

            Assert.True(result);
        }

        [Fact]
        public async Task ReturnsTrueWhenNameDoesNotConflict()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("butterfly");
            var pattern = new SkillPattern
            {
                Name = "buttefly",
                Pattern = "butterfly",
                Slug = "butterfly"
            };
            skill.Patterns.Add(pattern);
            await env.Db.SaveChangesAsync();
            var validator = env.Activate<PatternValidator>();

            var result = await validator.IsUniqueNameAsync(
                "cocoon pattern",
                pattern.Id,
                "cocoon",
                skill.Organization);

            Assert.True(result);
        }

        [Fact]
        public async Task ReturnsTrueWhenNameBelongsToCurrentEntity()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("butterfly");
            var pattern = new SkillPattern
            {
                Name = "buttefly wings",
                Slug = "butterfly-wings",
                Pattern = "butterfly"
            };
            skill.Patterns.Add(pattern);
            await env.Db.SaveChangesAsync();
            var validator = env.Activate<PatternValidator>();

            var result = await validator.IsUniqueNameAsync(
                "Butterfly Wings",
                pattern.Id,
                "butterfly",
                skill.Organization);

            Assert.True(result);
        }

        [Fact]
        public async Task ReturnsFalseWhenNameIsNotUniqueCaseInsensitive()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("butterfly");
            var pattern = new SkillPattern
            {
                Name = "butterfly WINGS",
                Slug = "butterfly-wings",
                Pattern = "butterfly"
            };
            skill.Patterns.Add(pattern);
            await env.Db.SaveChangesAsync();
            var validator = env.Activate<PatternValidator>();

            var result = await validator.IsUniqueNameAsync(
                "Butterfly Wings",
                0,
                "butterfly",
                skill.Organization);

            Assert.False(result);
        }
    }

    public class TheIsValidPatterMethod
    {
        [Theory]
        [InlineData(PatternType.None)]
        [InlineData(PatternType.Contains)]
        [InlineData(PatternType.StartsWith)]
        [InlineData(PatternType.EndsWith)]
        public void ReturnsTrueForNonRegexPatterns(PatternType patternType)
        {
            var env = TestEnvironment.Create();
            var validator = env.Activate<PatternValidator>();

            var result = validator.IsValidPattern("anything", patternType);

            Assert.True(result);
        }

        [Fact]
        public void ReturnsTrueValidRegularExpressionPattern()
        {
            var env = TestEnvironment.Create();
            var validator = env.Activate<PatternValidator>();

            var result = validator.IsValidPattern(".*", PatternType.RegularExpression);

            Assert.True(result);
        }

        [Fact]
        public void ReturnsFalseForInvalidRegularExpressionPattern()
        {
            var env = TestEnvironment.Create();
            var validator = env.Activate<PatternValidator>();

            var result = validator.IsValidPattern("(.*", PatternType.RegularExpression);

            Assert.False(result);
        }
    }
}
