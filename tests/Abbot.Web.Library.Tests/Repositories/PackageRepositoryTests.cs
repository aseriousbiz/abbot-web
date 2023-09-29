using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Xunit;

public class PackageRepositoryTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesAPackageUsingSkillNameUsageAndDescription()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill", CodeLanguage.Python);
            var createModel = new PackageUpdateModel
            {
                Readme = "The readme",
                ReleaseNotes = "The release notes"
            };
            var repository = env.Activate<PackageRepository>();

            var result = await repository.CreateAsync(createModel, skill, user);

            Assert.NotNull(result);
            Assert.Equal("The readme", result.Readme);
            Assert.Equal("test-skill", result.Skill.Name);
            Assert.Equal(skill.Description, result.Description);
            Assert.Equal(skill.UsageText, result.UsageText);
            Assert.Equal(skill.Code, result.Code);
            Assert.Equal(CodeLanguage.Python, result.Language);
            var version = result.Versions.Single();
            Assert.Equal(1, version.MajorVersion);
            Assert.Equal(0, version.MinorVersion);
            Assert.Equal(0, version.PatchVersion);
            Assert.Equal("The release notes", version.ReleaseNotes);
            var logEntry = await env.Db.AuditEvents.OfType<PackageEvent>().LastAsync();
            Assert.Equal("The readme", logEntry.Readme);
            Assert.Equal("Published package `test-skill` version `1.0.0`.", logEntry.Description);
        }
    }

    public class ThePublishNewVersionAsyncMethod
    {
        [Theory]
        [InlineData("Major", 2, 0, 0)]
        public async Task UpdatesPackageWithNewVersion(
            string changeType,
            int expectedMajor,
            int expectedMinor,
            int expectedPatch)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill", CodeLanguage.Python);
            var createModel = new PackageUpdateModel
            {
                Readme = "The readme",
                ReleaseNotes = "The release notes"
            };
            var repository = env.Activate<PackageRepository>();
            var package = await repository.CreateAsync(createModel, skill, user);
            skill.Name = "better-test-skill";
            skill.Code = "# Better comment";
            skill.Description = "A new improved cool skill";
            skill.UsageText = "More helpful usage instructions.";
            var updateModel = new PackageUpdateModel
            {
                Readme = "New improved readme",
                ReleaseNotes = "Breaking changes!",
                ChangeType = changeType
            };

            var result = await repository.PublishNewVersionAsync(
                updateModel,
                package,
                skill,
                user);

            Assert.NotNull(result);
            var updatedPackage = result.Package;
            Assert.Equal("New improved readme", updatedPackage.Readme);
            Assert.Equal("better-test-skill", updatedPackage.Skill.Name);
            Assert.Equal("A new improved cool skill", updatedPackage.Description);
            Assert.Equal("More helpful usage instructions.", updatedPackage.UsageText);
            Assert.Equal("# Better comment", updatedPackage.Code);
            Assert.Equal(CodeLanguage.Python, updatedPackage.Language);
            Assert.Equal(expectedMajor, result.MajorVersion);
            Assert.Equal(expectedMinor, result.MinorVersion);
            Assert.Equal(expectedPatch, result.PatchVersion);
            Assert.Equal("Breaking changes!", result.ReleaseNotes);
        }
    }

    public class TheUpdatePackageMetadataAsyncMethod
    {
        [Fact]
        public async Task UnlistsPackage()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill", CodeLanguage.Python);
            var createModel = new PackageUpdateModel
            {
                Readme = "The readme",
                ReleaseNotes = "The release notes"
            };
            var repository = env.Activate<PackageRepository>();
            var package = await repository.CreateAsync(createModel, skill, user);
            Assert.True(package.Listed);

            await repository.UpdatePackageMetadataAsync(package, "The readme", false, user);

            Assert.False(package.Listed);
            var logEntry = await env.Db.AuditEvents.OfType<PackageEvent>().LastAsync();
            Assert.Null(logEntry.Readme);
            Assert.Equal("Unlisted package `test-skill` version `1.0.0`.", logEntry.Description);
        }

        [Fact]
        public async Task LogsReadmeChange()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill", CodeLanguage.Python);
            var createModel = new PackageUpdateModel
            {
                Readme = "The readme",
                ReleaseNotes = "The release notes"
            };
            var repository = env.Activate<PackageRepository>();
            var package = await repository.CreateAsync(createModel, skill, user);
            Assert.True(package.Listed);

            await repository.UpdatePackageMetadataAsync(package, "The new readme", true, user);

            Assert.True(package.Listed);
            var logEntry = await env.Db.AuditEvents.OfType<PackageEvent>().LastAsync();
            Assert.Equal("The new readme", logEntry.Readme);
            Assert.Equal("Changed package `test-skill` version `1.0.0`.", logEntry.Description);
        }
    }
}
