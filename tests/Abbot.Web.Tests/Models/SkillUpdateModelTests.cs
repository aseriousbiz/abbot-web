using System;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Xunit;

public class SkillUpdateModelTests
{
    public class TheApplyMethod
    {
        [Fact]
        public void DoesNotApplyEmptyStringAsChangeForName()
        {
            var skill = new Skill { Name = "test", Description = "description" };
            var updateModel = new SkillUpdateModel
            {
                Name = "",
                Description = null,
                Code = "/* new code */"
            };

            updateModel.ApplyChanges(skill);

            Assert.Equal("test", skill.Name);
            Assert.Equal("description", skill.Description);
            Assert.Equal("/* new code */", skill.Code);
            Assert.Empty(skill.UsageText);
        }
    }

    public class TheToVersionSnapshotMethod
    {
        [Fact]
        public void ReturnsSkillVersionSnapshotForSkillPopulatedOnlyByChangedProperties()
        {
            var updateModel = new SkillUpdateModel
            {
                Name = "",
                Code = "/* NEW CODE */"
            };
            var skill = new Skill
            {
                Id = 42,
                Name = "test-skill",
                UsageText = "usage",
                Code = "/* code */",
                Description = "description",
                Creator = new User { Id = 1 },
                Created = DateTime.UtcNow.Subtract(TimeSpan.FromDays(2)),
                ModifiedBy = new User { Id = 2 },
                Modified = DateTime.UtcNow
            };
            var version = updateModel.ToVersionSnapshot(skill);

            Assert.Same(skill, version.Skill);
            Assert.Equal(skill.Id, version.SkillId);
            Assert.Null(version.Name);
            Assert.Equal("/* code */", version.Code);
            Assert.Null(version.Description);
            Assert.Equal(skill.Modified, version.Created);
            Assert.Equal(skill.ModifiedById, version.CreatorId);
        }
    }

    public class TheNameProperty
    {
        [Fact]
        public void SetsEmptyStringToNull()
        {
            var updateModel = new SkillUpdateModel { Name = "test" };
            Assert.Equal("test", updateModel.Name);
            updateModel.Name = "";
            Assert.Null(updateModel.Name);
        }
    }

}
