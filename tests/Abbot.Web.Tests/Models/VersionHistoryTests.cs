using System;
using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Xunit;

public class VersionHistoryTests
{
    public class TheGetVersionSnapshotMethod
    {
        [Fact]
        public void ReturnsStateOfSkillWhenNoVersions()
        {
            var skill = new Skill
            {
                Name = "invisibility",
                Code = "/* Code */",
                UsageText = "How to do things.",
                Description = "What it is."
            };
            var versionHistory = new VersionHistory(skill);

            var snapshot = versionHistory.GetVersionSnapshot(1);

            Assert.Equal("invisibility", snapshot.Name);
            Assert.Equal("/* Code */", snapshot.Code);
            Assert.Equal("How to do things.", snapshot.UsageText);
            Assert.Equal("What it is.", snapshot.Description);
        }

        [Fact]
        public void ReturnsStateOfSkillForFirstVersion()
        {
            var skill = new Skill
            {
                Name = "invisibility",
                Code = "/* New code */",
                UsageText = "/* Updated Usage Text */",
                Description = "What it is.",
                Versions = new List<SkillVersion>
                {
                    new SkillVersion { UsageText = "How to do things", Created = DateTime.UtcNow },
                    new SkillVersion { Code = "/* original code */", Created = DateTime.UtcNow.AddDays(-1) }
                }
            };
            var versionHistory = new VersionHistory(skill);

            var snapshot = versionHistory.GetVersionSnapshot(1);

            Assert.Equal("invisibility", snapshot.Name);
            Assert.Equal("/* original code */", snapshot.Code);
            Assert.Equal("How to do things", snapshot.UsageText);
            Assert.Equal("What it is.", snapshot.Description);
        }
    }
}
