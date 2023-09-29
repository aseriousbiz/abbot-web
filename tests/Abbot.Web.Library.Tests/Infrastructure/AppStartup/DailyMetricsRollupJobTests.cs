using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.TestHelpers;
using Xunit;

public class DailyMetricsRollupJobTests
{
    public class TheRunAsyncMethod
    {
        [Fact]
        public async Task RollsUpAllDaysSinceLastRollup()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var anotherMember = await env.CreateMemberAsync();
            var today = DateTime.UtcNow.Date;
            var lastRollup = new DailyMetricsRollup
            {
                Date = today.AddDays(-3).Date
            };
            await env.Db.DailyMetricsRollups.AddAsync(lastRollup);
            var skill = await env.CreateSkillAsync("random-skill");
            skill.Created = today.AddDays(-2);
            var org = await env.CreateOrganizationAsync("random-org");
            org.Created = today.AddDays(-1);
            var member = await env.CreateMemberAsync();
            member.User.NameIdentifier = "something";
            member.User.Created = today.AddDays(-3);
            // Add some interactions
            await env.Db.AuditEvents.AddRangeAsync(new BuiltInSkillRunEvent
            {
                SkillName = "some-skill",
                Organization = organization,
                Arguments = "some-args",
                Actor = user,
                Description = "whatever",
                Created = today.AddDays(-2)
            },
                new BuiltInSkillRunEvent
                {
                    SkillName = "some-skill",
                    Organization = organization,
                    Arguments = "some-args",
                    Actor = user,
                    Description = "whatever",
                    Created = today.AddDays(-2)
                },
                new BuiltInSkillRunEvent
                {
                    SkillName = "some-skill",
                    Organization = organization,
                    Arguments = "some-args",
                    Actor = user,
                    Description = "whatever",
                    Created = today.AddDays(-1)
                },
                new BuiltInSkillRunEvent
                {
                    SkillName = "some-skill",
                    Organization = organization,
                    Arguments = "some-args",
                    Actor = anotherMember.User,
                    Description = "whatever",
                    Created = today.AddDays(-1)
                },
                new BuiltInSkillRunEvent
                {
                    SkillName = "some-skill",
                    Organization = organization,
                    Arguments = "some-args",
                    Actor = anotherMember.User,
                    Description = "whatever",
                    Created = today.AddDays(-1)
                });
            await env.Db.SaveChangesAsync();
            var job = new DailyMetricsRollupJob(env.Db);

            await job.RunAsync();

            var metrics = await env.Db.DailyMetricsRollups.ToListAsync();
            Assert.Equal(3, metrics.Count);
            Assert.Equal(metrics[0].Date, today.AddDays(-3).Date);
            Assert.Equal(metrics[0].InteractionCount, 0);
            Assert.Equal(metrics[0].ActiveUserCount, 0);
            Assert.Equal(metrics[0].SkillCreatedCount, 0);
            Assert.Equal(metrics[0].OrganizationCreatedCount, 0);
            Assert.Equal(metrics[0].UserCreatedCount, 1);
            Assert.Equal(metrics[1].Date, today.AddDays(-2).Date);
            Assert.Equal(metrics[1].InteractionCount, 2);
            Assert.Equal(metrics[1].ActiveUserCount, 1);
            Assert.Equal(metrics[1].SkillCreatedCount, 1);
            Assert.Equal(metrics[1].OrganizationCreatedCount, 0);
            Assert.Equal(metrics[1].UserCreatedCount, 0);
            Assert.Equal(metrics[2].Date, today.AddDays(-1).Date);
            Assert.Equal(metrics[2].InteractionCount, 3);
            Assert.Equal(metrics[2].ActiveUserCount, 2);
            Assert.Equal(metrics[2].SkillCreatedCount, 0);
            Assert.Equal(metrics[2].OrganizationCreatedCount, 1);
            Assert.Equal(metrics[2].UserCreatedCount, 0);
        }

        [Fact]
        public async Task DoesNothingIfNoRecords()
        {
            var db = new FakeAbbotContext();
            var job = new DailyMetricsRollupJob(db);

            await job.RunAsync();

            var metrics = await db.DailyMetricsRollups.ToListAsync();
            Assert.Empty(metrics);
        }
    }
}
