using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Skills;
using Serious.Abbot.Validation;
using Xunit;

public class SkillNameValidatorTests
{
    public class TheIsUniqueNameMethod
    {
        [Fact]
        public async Task ReturnsTrueWhenNameIsUniqueOrBelongsToCurrentEntity()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var skillNameValidator = env.Activate<SkillNameValidator>();

            var result = await skillNameValidator.IsUniqueNameAsync(
                "test",
                0,
                nameof(Skill),
                organization);

            Assert.True(result.IsUnique);
        }

        [Fact]
        public async Task ReturnsTrueWhenNameBelongsToCurrentEntity()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var skill = await env.CreateSkillAsync("fred");
            var skillNameValidator = env.Activate<SkillNameValidator>();

            var result = await skillNameValidator.IsUniqueNameAsync(
                "fred",
                skill.Id,
                nameof(Skill),
                organization);

            Assert.True(result.IsUnique);
        }

        [Fact]
        public async Task ReturnsFalseWhenNameIsNotUniqueForSameType()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var skill = await env.CreateSkillAsync("fred");
            var skillNameValidator = env.Activate<SkillNameValidator>();

            var result = await skillNameValidator.IsUniqueNameAsync(
                "fred",
                skill.Id + 1,
                nameof(Skill),
                organization);

            Assert.False(result.IsUnique);
            Assert.Equal(nameof(Skill), result.ConflictType);
        }

        [Fact]
        public async Task ReturnsFalseWhenNameMatchesBuiltIn()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            var skillNameValidator = env.Activate<SkillNameValidator>();

            var result = await skillNameValidator.IsUniqueNameAsync(
                "echo",
                0,
                nameof(Skill),
                organization);

            Assert.False(result.IsUnique);
            Assert.Equal("ISkill", result.ConflictType);
        }

        [Fact]
        public async Task ReturnsFalseWhenNameIsReserved()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            var skillNameValidator = env.Activate<SkillNameValidator>();

            var result = await skillNameValidator.IsUniqueNameAsync(
                "install",
                0,
                nameof(Skill),
                organization);

            Assert.False(result.IsUnique);
            Assert.Equal("Reserved", result.ConflictType);
        }

        [Fact]
        public async Task ReturnsFalseWhenNameIsNotUniqueForDifferentTypeEvenIfIdsMatch()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var skill = await env.CreateSkillAsync("fred");
            env.BuiltinSkillRegistry.AddSkill(new EchoSkill());
            var skillNameValidator = env.Activate<SkillNameValidator>();

            var result = await skillNameValidator.IsUniqueNameAsync(
                "fred",
                skill.Id,
                nameof(UserList),
                organization);

            Assert.False(result.IsUnique);
            Assert.Equal(nameof(Skill), result.ConflictType);
        }
    }
}
