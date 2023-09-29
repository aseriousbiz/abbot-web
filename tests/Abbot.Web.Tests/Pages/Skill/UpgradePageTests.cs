using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Skills;
using Xunit;

public class UpgradePageTests : PageTestBase<UpgradeModel>
{
    public class TheOnPostAsyncMethod : UpgradePageTests
    {
        [Fact]
        public async Task UpdatesSkillWithLatestPackageChanges()
        {
            var env = Env;
            var user = env.TestData.User;
            var now = DateTime.UtcNow;
            var then = now.AddHours(-1);
            var sourceSkill = await env.CreateSkillAsync("test-package", usageText: "how to use this skill", description: "Test Description");
            var skill = await env.CreateSkillAsync("test");
            skill.SourcePackageVersion = new PackageVersion
            {
                Package = new Package
                {
                    Organization = sourceSkill.Organization,
                    Skill = sourceSkill,
                    Created = then,
                    Creator = user,
                    Modified = then,
                    ModifiedBy = user,
                    Versions = new List<PackageVersion>
                    {
                        new()
                        {
                            Created = then,
                            Creator = user,
                            MajorVersion = 1,
                            MinorVersion = 0,
                            PatchVersion = 0,
                            CodeCacheKey = "some-key"
                        }
                    }
                },
                CodeCacheKey = "some-key"
            };
            var package = skill.SourcePackageVersion.Package;
            package.Versions.Add(new PackageVersion
            {
                MajorVersion = 2,
                MinorVersion = 0,
                PatchVersion = 0,
                Created = now,
                Creator = user,
                CodeCacheKey = "the-key"
            });
            package.Code = "/* Updated code */";
            package.UsageText = "Updated usage";
            package.Description = "Updated description";
            await env.Db.SaveChangesAsync();

            var (_, result) = await InvokePageAsync<RedirectResult>(p => p.OnPostAsync(skill.Name));

            Assert.Equal("/skills/test", result.Url);
            Assert.Equal("test", skill.Name);
            Assert.Equal("/* Updated code */", skill.Code);
            Assert.Equal("Updated usage", skill.UsageText);
            Assert.Equal("Updated description", skill.Description);
        }
    }
}
