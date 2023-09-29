using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Scripting;
using Serious.Abbot.Skills;
using Serious.Abbot.Telemetry;
using Xunit;

public class AuditLogTests
{
    public class TheLogEntityCreatedAsyncMethod
    {
        [Fact]
        public async Task LogsTriggerCreation()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.User;
            var trigger = new SkillHttpTrigger
            {
                Name = "some-trigger",
                ApiToken = "secret-token",
                Skill = new Skill { Name = "some-skill", Organization = organization },
                RoomId = "C001",
            };
            var auditLog = env.Activate<AuditLog>();

            var entry = await auditLog.LogEntityCreatedAsync(trigger, actor, organization);

            Assert.NotNull(entry);
            Assert.Equal("Created HTTP trigger `some-trigger` for skill `some-skill`.", entry.Description);
        }

        [Fact]
        public async Task LogsSecretCreationAsSkillEvent()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.User;
            var secret = new SkillSecret
            {
                Name = "super-secret",
                KeyVaultSecretName = "not-your-biz",
                Description = "A secret",
                Skill = new Skill { Name = "test", Organization = organization },
                Organization = organization
            };
            var auditLog = env.Activate<AuditLog>();

            var entry = await auditLog.LogEntityCreatedAsync(secret, actor, organization);

            Assert.NotNull(entry);
            Assert.Equal("Created secret `super-secret` for skill `test`.", entry.Description);
            var lastEvent = await env.Db.AuditEvents.OfType<SkillAuditEvent>().LastAsync();
            Assert.Equal(entry.Description, lastEvent.Description);
        }

        [Fact]
        public async Task LogsListEntryCreation()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.User;
            var listEntry = new UserListEntry
            {
                List = new UserList { Name = "joke", Organization = organization },
                Content = "ha ha"
            };
            var auditLog = env.Activate<AuditLog>();

            var entry = await auditLog.LogEntityCreatedAsync(listEntry, actor, organization);

            Assert.NotNull(entry);
            Assert.Equal("Added `ha ha` to list `joke`.", entry.Description);
        }
    }

    public class TheLogEntityChangedAsyncMethod
    {
        [Fact]
        public async Task LogsTriggerScheduleChange()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.User;
            var trigger = new SkillScheduledTrigger
            {
                Name = "some-trigger",
                CronSchedule = "*/20 * * * *",
                Skill = new Skill { Name = "some-skill", Organization = organization }
            };
            var auditLog = env.Activate<AuditLog>();

            var entry = await auditLog.LogEntityChangedAsync(trigger, actor, organization);

            Assert.NotNull(entry);
            Assert.Equal("Changed scheduled trigger `some-trigger` with schedule `Every 20 minutes, every hour, every day` for skill `some-skill`.", entry.Description);
        }
    }

    public class TheLogEntityDeletedAsyncMethod
    {
        [Fact]
        public async Task LogsTriggerCreation()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.User;
            var trigger = new SkillHttpTrigger
            {
                Name = "some-trigger",
                ApiToken = "secret",
                Skill = new Skill { Name = "some-skill", Organization = organization },
                RoomId = "C001",
            };
            var auditLog = env.Activate<AuditLog>();

            var entry = await auditLog.LogEntityDeletedAsync(trigger, actor, organization);

            Assert.NotNull(entry);
            Assert.Equal("Removed HTTP trigger `some-trigger` for skill `some-skill`.", entry.Description);
        }
    }

    public class TheLogPackagePublishedAsyncMethod
    {
        [Fact]
        public async Task LogsPackageCreation()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.User;
            var package = new Package
            {
                Id = 23,
                Readme = "bla bla bla",
                Listed = true,
                Skill = new Skill { Id = 42, Name = "some-skill", Organization = organization },
                Versions = new List<PackageVersion> { new() { MajorVersion = 1, MinorVersion = 0, PatchVersion = 2 } }
            };
            var auditLog = env.Activate<AuditLog>();

            await auditLog.LogPackagePublishedAsync(package, actor, organization);

            var packageEvent = await env.AuditLog.AssertMostRecent<PackageEvent>(
                "Published package `some-skill` version `1.0.2`.");
            Assert.Equal("bla bla bla", packageEvent.Readme);
            Assert.Equal(42, packageEvent.SkillId);
            Assert.Equal(23, packageEvent.EntityId);
        }
    }

    public class TheLogPackageChangedAsyncMethod
    {
        [Fact]
        public async Task LogsPackageReadmeChange()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.User;
            var package = new Package
            {
                Id = 23,
                Readme = "bla bla bla",
                Listed = true,
                Skill = new Skill { Id = 42, Name = "some-skill", Organization = organization },
                Versions = new List<PackageVersion> { new() { MajorVersion = 1, MinorVersion = 0, PatchVersion = 2 } }
            };
            var auditLog = env.Activate<AuditLog>();

            await auditLog.LogPackageChangedAsync(package, actor, organization);

            var entry = Assert.Single(await env.Db.AuditEvents.OfType<PackageEvent>().ToListAsync());
            Assert.Equal("Changed package `some-skill` version `1.0.2`.", entry.Description);
            Assert.Equal("bla bla bla", entry.Readme);
        }
    }

    public class TheLogBuiltInSkillRunAsyncMethod
    {
        [Theory]
        [InlineData("joke add sensitive info", "Added an item to the `joke` list in `#test-room` (`room-id`)")]
        [InlineData("joke remove sensitive info", "Removed an item from the `joke` list in `#test-room` (`room-id`)")]
        [InlineData("joke list", "Listed all items in the `joke` list in `#test-room` (`room-id`)")]
        [InlineData("joke info", "Displayed info about the `joke` list in `#test-room` (`room-id`)")]
        public async Task SpecialCasesListSkill(string args, string expected)
        {
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var messageContext = env.CreateFakeMessageContext(
                "joke",
                args,
                room: new Room { PlatformRoomId = "room-id", Name = "test-room" });
            var listSkill = env.Activate<ListSkill>();

            var entry = await env.AuditLog.LogBuiltInSkillRunAsync(listSkill, messageContext);

            Assert.NotNull(entry);
            Assert.Equal(expected, entry.Description);
            Assert.Equal("test-room", entry.Room);
            Assert.Equal("room-id", entry.RoomId);
        }
    }

    public class TheLogPackageUnlistedAsyncMethod
    {
        [Fact]
        public async Task LogsPackageUnlisted()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.User;
            var package = new Package
            {
                Id = 23,
                Readme = "bla bla bla",
                Listed = true,
                Skill = new Skill { Id = 42, Name = "some-skill", Organization = organization },
                Versions = new List<PackageVersion> { new() { MajorVersion = 1, MinorVersion = 0, PatchVersion = 2 } }
            };
            var auditLog = env.Activate<AuditLog>();

            await auditLog.LogPackageUnlistedAsync(package, actor, organization);

            var entry = Assert.Single(await env.Db.AuditEvents.OfType<PackageEvent>().ToListAsync());
            Assert.Equal("Unlisted package `some-skill` version `1.0.2`.", entry.Description);
            Assert.Null(entry.Readme);
        }
    }

    public class TheLogInstalledAbbotAsyncMethod
    {
        [Theory]
        [InlineData(null, PlatformType.UnitTest, "Installed Abbot to the Test Organization UnitTest.")]
        [InlineData("Custom Abbot", PlatformType.Slack, "Installed Custom Abbot to the Test Organization Slack.")]
        public async void LogsAppNameFromInstallationInfo(string? appName, PlatformType platformType, string expected)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;

            var info = InstallationInfo.Create(InstallationEventAction.Install, organization) with
            {
                AppName = appName,
                PlatformType = platformType,
            };

            var auditLog = env.Activate<AuditLog>();

            await auditLog.LogInstalledAbbotAsync(info, organization, actor);

            var entry = Assert.IsType<InstallationEvent>(await env.Db.AuditEvents.LastAsync());
            Assert.Equal(expected, entry.Description);

            var installationInfo = entry.ReadProperties<InstallationInfo>();
            Assert.Equal(info, installationInfo);

            env.AnalyticsClient.AssertTracked("Slack App Installed", AnalyticsFeature.Activations, actor,
                new {
                    app_id = info.AppId,
                });
        }
    }

    public class TheLogUninstalledAbbotAsyncMethod
    {
        [Theory]
        [InlineData(null, PlatformType.UnitTest, "Uninstalled Abbot from the Test Organization UnitTest.")]
        [InlineData("Custom Abbot", PlatformType.Slack, "Uninstalled Custom Abbot from the Test Organization Slack.")]
        public async void LogsAppNameFromInstallationInfo(string? appName, PlatformType platformType, string expected)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;

            var info = InstallationInfo.Create(InstallationEventAction.Uninstall, organization) with
            {
                AppName = appName,
                PlatformType = platformType,
            };

            var auditLog = env.Activate<AuditLog>();

            await auditLog.LogUninstalledAbbotAsync(info, organization, actor);

            var entry = Assert.IsType<InstallationEvent>(await env.Db.AuditEvents.LastAsync());
            Assert.Equal(expected, entry.Description);

            var installationInfo = entry.ReadProperties<InstallationInfo>();
            Assert.Equal(info, installationInfo);

            env.AnalyticsClient.AssertTracked("Slack App Uninstalled", AnalyticsFeature.Activations, actor,
                new {
                    app_id = info.AppId,
                });
        }
    }
}
