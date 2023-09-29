using Abbot.Common.TestHelpers;
using Hangfire;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.TestHelpers;

public class ScheduledSkillClientTests
{
    // ReSharper disable once ClassNeverInstantiated.Local
    class TestData : CommonTestData
    {
        public Skill Skill { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            var skill = new Skill
            {
                Name = "pug",
                Organization = env.TestData.Organization
            };
            Skill = await env.Skills.CreateAsync(skill, env.TestData.User);
        }
    }

    public class TheRunScheduledSkillMethod
    {
        [Fact]
        public async Task RunsSkill()
        {
            var env = TestEnvironment.Create<TestData>();
            var skill = env.TestData.Skill;
            var trigger = await env.Triggers.CreateScheduledTriggerAsync(
                skill,
                "bomb",
                "Some description",
                Cron.Daily(4),
                new PlatformRoom("the-cool-room-id", "the-cool-room"),
                env.TestData.Member);
            var skillRunnerClient = env.Get<ISkillRunnerClient>() as FakeSkillRunnerClient;
            var client = env.Activate<ScheduledSkillClient>();

            await client.RunScheduledSkillAsync(trigger.Id, "ignored", 0, "ignored", 0);

            Assert.Equal("the-cool-room", trigger.Name);
            var invocation = Assert.Single(skillRunnerClient!.Invocations);
            Assert.NotNull(invocation);
            Assert.Same(trigger, invocation.SkillTrigger);
            Assert.NotNull(invocation.Caller);
            Assert.Equal(env.TestData.Member.Id, invocation.Caller.Id);
        }

        [Fact]
        public async Task DoesNotRunDisabledSkill()
        {
            var env = TestEnvironment.Create<TestData>();
            var skill = env.TestData.Skill;
            skill.Enabled = false;
            await env.Db.SaveChangesAsync();
            var trigger = await env.Triggers.CreateScheduledTriggerAsync(
                skill,
                "bomb",
                "Some description",
                Cron.Daily(4),
                new PlatformRoom("the-cool-room-id", "the-cool-room"),
                env.TestData.Member);
            var skillRunnerClient = env.Get<ISkillRunnerClient>() as FakeSkillRunnerClient;
            var client = env.Activate<ScheduledSkillClient>();

            await client.RunScheduledSkillAsync(trigger.Id, "ignored", 0, "ignored", 0);

            Assert.Equal("the-cool-room", trigger.Name);
            Assert.Empty(skillRunnerClient!.Invocations);
        }

        [Fact]
        public async Task RemovesRecurringJobIfSkillTriggerDoesNotExist()
        {
            var env = TestEnvironment.Create();
            var recurringJobManager = env.Get<IRecurringJobManager>() as FakeRecurringJobManager;
            var client = env.Activate<ScheduledSkillClient>();
            var trigger = new SkillScheduledTrigger
            {
                Id = 42,
                CronSchedule = Cron.Daily(4),
                Skill = new Skill
                {
                    Id = 0,
                    Name = "ignored",
                    Organization = new Organization
                    {
                        Id = 0,
                        Name = "ignored"
                    }
                }
            };
            var jobId = client.ScheduleSkill(trigger);
            Assert.True(recurringJobManager!.RecurringJobs.ContainsKey(jobId));

            await client.RunScheduledSkillAsync(trigger.Id, "ignored", 0, "ignored", 0);

            Assert.False(recurringJobManager.RecurringJobs.ContainsKey(jobId));
        }
    }

    public class TheScheduleSkillMethod
    {
        [Fact]
        public void SchedulesTheSkillToBeCalled()
        {
            var env = TestEnvironment.Create();
            var trigger = new SkillScheduledTrigger
            {
                Id = 42,
                CronSchedule = Cron.Daily(4),
                Skill = new Skill
                {
                    Id = 0,
                    Name = "ignored",
                    Organization = new Organization
                    {
                        Id = 0,
                        Name = "ignored"
                    }
                }
            };
            var recurringJobManager = env.Get<IRecurringJobManager>() as FakeRecurringJobManager;
            var client = env.Activate<ScheduledSkillClient>();

            var jobId = client.ScheduleSkill(trigger);

            Assert.Equal("ScheduledSkill_42", jobId);
            var (job, cron) = Assert.Single(recurringJobManager!.RecurringJobs.Values);
            Assert.Equal(nameof(ScheduledSkillClient.RunScheduledSkillAsync), job.Method.Name);
            Assert.Equal(42, job.Args[0]);
            Assert.Equal(Cron.Daily(4), cron);
        }
    }
}
