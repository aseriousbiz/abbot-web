using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Abbot.Telemetry;
using Serious.TestHelpers.CultureAware;
using Xunit;

public class SkillAuditLogTests
{
    public class TheLogSkillEditEventAsyncMethod
    {
        [Fact]
        [UseCulture("en-US")]
        public async Task LogsPropertiesChanged()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var version = new SkillVersion
            {
                Creator = user,
                UsageText = "Old Usage Text",
                Description = "Old Description",
                Skill = skill
            };
            await env.Db.SaveChangesAsync();
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillChangedAsync(skill, version, user);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillInfoChangedAuditEvent>().LastAsync();
            Assert.Equal("Changed properties `Description` and `Usage` of skill `test`.", auditEvent.Description);
        }

        [Fact]
        public async Task LogsPropertyChanged()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var version = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                UsageText = "Old Usage Text"
            };
            skill.Versions.Add(version);
            await env.Db.SaveChangesAsync();
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillChangedAsync(skill, version, user);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillInfoChangedAuditEvent>().LastAsync();
            Assert.Equal("Changed property `Usage` of skill `test`.", auditEvent.Description);
        }

        [Fact]
        [UseCulture("en-US")]
        public async Task LogsPropertiesChangedWhenTheyWereBlankBefore()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var version = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                UsageText = "",
                Description = ""
            };
            skill.Versions.Add(version);
            await env.Db.SaveChangesAsync();
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillChangedAsync(skill, version, user);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillInfoChangedAuditEvent>().LastAsync();
            Assert.Equal("Changed properties `Description` and `Usage` of skill `test`.", auditEvent.Description);
        }

        [Fact]
        public async Task LogsSpecificMessageForRename()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test2");
            var version = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Name = "test"
            };
            await env.Db.SaveChangesAsync();
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillChangedAsync(skill, version, user);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillInfoChangedAuditEvent>().LastAsync();
            Assert.Equal("Renamed skill `test` to `test2`.", auditEvent.Description);
        }

        [Theory]
        [InlineData(true, false, "Unrestricted skill `test`.")]
        [InlineData(false, true, "Restricted skill `test`.")]
        public async Task LogsSpecificMessageForRestricting(bool original, bool current, string expected)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            skill.Restricted = current;
            var version = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Restricted = original
            };
            skill.Versions.Add(version);
            await env.Db.SaveChangesAsync();
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillChangedAsync(skill, version, user);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillInfoChangedAuditEvent>().LastAsync();
            Assert.Equal(expected, auditEvent.Description);
        }

        [Fact]
        public async Task CreatesSkillEditSessionEvent()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var version = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Code = "some changes"
            };
            skill.Versions.Add(version);
            await env.Db.SaveChangesAsync();
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillChangedAsync(skill, version, user);

            var entry = await env.Db.AuditEvents.LastAsync();
            Assert.NotNull(entry);
            Assert.Equal("Edited C# Code of skill `test`.", entry.Description);
            var session = Assert.IsType<SkillEditSessionAuditEvent>(entry);
            Assert.Equal(version.Id, session.FirstSkillVersionId);
            Assert.Equal(1, session.EditCount);
        }

        [Fact]
        public async Task CreatesSkillEditSessionWithMultipleEvents()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var version1 = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Code = "some changes",
                Created = new DateTime(2020, 1, 23, 4, 20, 4)
            };
            var version2 = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Code = "more changes",
                Created = new DateTime(2020, 1, 23, 4, 22, 0)
            };
            var version3 = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Code = "even changes",
                Created = new DateTime(2020, 1, 23, 4, 24, 52)
            };
            skill.Versions.Add(version1);
            skill.Versions.Add(version2);
            skill.Versions.Add(version3);
            await env.Db.SaveChangesAsync();
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillChangedAsync(skill, version1, user);
            var initial = await env.Db.AuditEvents.OfType<SkillEditSessionAuditEvent>().LastAsync();
            await auditLog.LogSkillChangedAsync(skill, version2, user);
            var second = await env.Db.AuditEvents.OfType<SkillEditSessionAuditEvent>().LastAsync();
            await auditLog.LogSkillChangedAsync(skill, version3, user);
            var final = await env.Db.AuditEvents.OfType<SkillEditSessionAuditEvent>().LastAsync();

            Assert.NotNull(final);
            Assert.Equal(initial.Id, second.Id);
            Assert.Equal(initial.Id, final.Id);
            Assert.Equal(
                "Edited C# Code of skill `test` 3 times for a span of `4 minutes and 48 seconds`.",
                final.Description);
            var session = Assert.IsType<SkillEditSessionAuditEvent>(final);
            Assert.Equal(version1.Id, session.FirstSkillVersionId);
            Assert.Equal(3, session.EditCount);
        }

        [Fact]
        public async Task CreatesNewSkillEditSessionWhenEventsAreOverTenMinutesApart()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var version1 = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Code = "some changes",
                Created = new DateTime(2020, 1, 23, 4, 20, 4)
            };
            var version2 = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Code = "more changes",
                Created = new DateTime(2020, 1, 23, 4, 22, 5)
            };
            var version3 = new SkillVersion
            {
                Skill = skill,
                Creator = user,
                Code = "even changes",
                Created = new DateTime(2020, 1, 23, 4, 33, 52)
            };
            skill.Versions.Add(version1);
            skill.Versions.Add(version2);
            skill.Versions.Add(version3);
            await env.Db.SaveChangesAsync();
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillChangedAsync(skill, version1, user);
            var initial = await env.Db.AuditEvents.OfType<SkillEditSessionAuditEvent>().LastAsync();
            await auditLog.LogSkillChangedAsync(skill, version2, user);
            var second = await env.Db.AuditEvents.OfType<SkillEditSessionAuditEvent>().LastAsync();
            await auditLog.LogSkillChangedAsync(skill, version3, user);
            var final = await env.Db.AuditEvents.OfType<SkillEditSessionAuditEvent>().LastAsync();

            Assert.NotNull(final);
            Assert.Equal(initial.Id, second.Id);
            Assert.NotEqual(second.Id, final.Id);
            Assert.Equal(
                "Edited C# Code of skill `test` 2 times for a span of `2 minutes and 1 second`.",
                second.Description);
            Assert.Equal("Edited C# Code of skill `test`.", final.Description);
        }
    }

    public class TheLogSkillRunAsyncMethod
    {
        [Theory]
        [InlineData("", "Ran command `test remote-args` in _a channel with an unknown name_ (`room-id`).")]
        [InlineData("some-room", "Ran command `test remote-args` in `#some-room` (`room-id`).")]
        public async Task LogsSkillRunCorrectly(string roomName, string expected)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var args = new Arguments("remote-args");
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillRunAsync(
                skill,
                args,
                null,
                null,
                user,
                new PlatformRoom("room-id", roomName),
                new SkillRunResponse { Success = true },
                Guid.NewGuid(),
                null);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillRunAuditEvent>().LastAsync();
            Assert.Equal("remote-args", auditEvent.Arguments);
            Assert.Equal(expected, auditEvent.Description);
        }

        [Theory]
        [InlineData("", "Ran command `test` with no arguments in _a channel with an unknown name_ (`the-cool-room-id`).")]
        [InlineData("some-room", "Ran command `test` with no arguments in `#some-room` (`the-cool-room-id`).")]
        public async Task LogsSkillRunWithNoArgsCorrectly(string roomName, string expected)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            // Remember, the args are actually for the RemoteSkillCallSkill skill.
            var args = new Arguments(string.Empty);
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillRunAsync(
                skill,
                args,
                null,
                null,
                user,
                new PlatformRoom("the-cool-room-id", roomName),
                new SkillRunResponse { Success = true },
                Guid.NewGuid(),
                null);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillRunAuditEvent>().LastAsync();
            Assert.Equal("", auditEvent.Arguments);
            Assert.Equal(expected, auditEvent.Description);
        }

        [Theory]
        [InlineData(PatternType.StartsWith, true, "`the-pattern` matches messages that start with `foobar` - case sensitive")]
        [InlineData(PatternType.EndsWith, true, "`the-pattern` matches messages that end with `foobar` - case sensitive")]
        [InlineData(PatternType.Contains, true, "`the-pattern` matches messages that contain `foobar` - case sensitive")]
        [InlineData(PatternType.RegularExpression, true, "`the-pattern` matches messages that match the regular expression `foobar` - case sensitive")]
        [InlineData(PatternType.StartsWith, false, "`the-pattern` matches messages that start with `foobar` - case insensitive")]
        [InlineData(PatternType.EndsWith, false, "`the-pattern` matches messages that end with `foobar` - case insensitive")]
        [InlineData(PatternType.Contains, false, "`the-pattern` matches messages that contain `foobar` - case insensitive")]
        [InlineData(PatternType.RegularExpression, false, "`the-pattern` matches messages that match the regular expression `foobar` - case insensitive")]
        public async Task LogsSkillRunDueToPatternMatchCorrectly(PatternType patternType, bool caseSensitive, string expected)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var args = new Arguments("remote-args");
            var pattern = new SkillPattern
            {
                Name = "the-pattern",
                Pattern = "foobar",
                PatternType = patternType,
                CaseSensitive = caseSensitive
            };
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillRunAsync(
                skill,
                args,
                pattern,
                null,
                user,
                new PlatformRoom("room-id", "the-room"),
                new SkillRunResponse { Success = true },
                Guid.NewGuid(),
                null);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillRunAuditEvent>().LastAsync();
            Assert.Equal("remote-args", auditEvent.Arguments);
            Assert.Equal($"Ran skill `test` in `#the-room` (`room-id`) due to pattern `the-pattern`.", auditEvent.Description);
            Assert.Equal(expected, auditEvent.PatternDescription);
        }

        [Fact]
        public async Task LogsSkillRunDueToSignalCorrectly()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test");
            var args = new Arguments("remote-args");
            var auditLog = env.Activate<SkillAuditLog>();

            await auditLog.LogSkillRunAsync(
                skill,
                args,
                null,
                "blinky-light",
                user,
                new PlatformRoom("room-id", "the-room"),
                new SkillRunResponse { Success = true },
                Guid.NewGuid(),
                null);

            var auditEvent = await env.Db.AuditEvents.OfType<SkillRunAuditEvent>().LastAsync();
            Assert.Equal("remote-args", auditEvent.Arguments);
            Assert.Equal($"Ran skill `test` in `#the-room` (`room-id`) in response to the signal `blinky-light`.", auditEvent.Description);
            Assert.Equal("blinky-light", auditEvent.Signal);
        }
    }
}
