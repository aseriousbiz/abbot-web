using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;

public class TriggerRepositoryTests
{
    public class TheGetSkillHttpTriggerAsyncMethod
    {
        [Fact]
        public async Task RetrievesTriggerByNameAndApiToken()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var skill = await env.CreateSkillAsync("test-skill");
            var triggerRepository = env.Activate<TriggerRepository>();

            var trigger = await triggerRepository.CreateHttpTriggerAsync(
                skill,
                "",
                new PlatformRoom("blah", "The Room"),
                member);

            var result = await triggerRepository.GetSkillHttpTriggerAsync(skill.Name, trigger.ApiToken);

            Assert.NotNull(result);
            Assert.Equal("", result.Description);
            Assert.Equal("The Room", result.Name);
            Assert.Equal("test-skill", result.Skill.Name);
        }
    }

    public class TheCreateHttpTriggerMethod
    {
        [Fact]
        public async Task CreatesHttpTriggerWithGeneratedApiToken()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var skill = await env.CreateSkillAsync("test-skill");
            var triggerRepository = env.Activate<TriggerRepository>();

            var trigger = await triggerRepository.CreateHttpTriggerAsync(
                skill,
                "A webhook",
                new PlatformRoom("the-room-id", "The Room"),
                member);

            Assert.NotNull(trigger);
            Assert.Same(skill, trigger.Skill);
            Assert.NotEmpty(trigger.ApiToken);
            Assert.Equal("The Room", trigger.Name);
            Assert.Equal("the-room-id", trigger.RoomId);
            Assert.Equal("A webhook", trigger.Description);
            var logEntry = await env.Db.AuditEvents.OfType<HttpTriggerChangeEvent>().LastAsync();
            Assert.Equal("Created HTTP trigger `The Room` for skill `test-skill`.", logEntry.Description);
            Assert.Equal(trigger.Id, logEntry.EntityId);
            Assert.Equal(skill.Id, logEntry.SkillId);
            Assert.Equal(trigger.Description, logEntry.TriggerDescription);
        }
    }

    public class TheCreateScheduledTriggerMethod
    {
        [Fact]
        public async Task CreatesScheduledTriggerWithUserTimeZoneId()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            member.TimeZoneId = "America/Phoenix";
            await env.Db.SaveChangesAsync();
            var skill = await env.CreateSkillAsync("test-skill");
            var triggerRepository = env.Activate<TriggerRepository>();

            var trigger = await triggerRepository.CreateScheduledTriggerAsync(
                skill,
                "args",
                "A webhook",
                "0 0 * * *",
                new PlatformRoom("room-id", "The Room"),
                member);

            Assert.NotNull(trigger);
            Assert.Same(skill, trigger.Skill);
            Assert.Equal("America/Phoenix", trigger.TimeZoneId);
            Assert.Equal("The Room", trigger.Name);
            Assert.Equal("room-id", trigger.RoomId);
            Assert.Equal("0 0 * * *", trigger.CronSchedule);
            Assert.Equal("A webhook", trigger.Description);
            var logEntry = await env.Db.AuditEvents.OfType<ScheduledTriggerChangeEvent>().LastAsync();
            Assert.Equal("Created scheduled trigger `The Room` with schedule `At 12:00 AM, every day` for skill `test-skill`.", logEntry.Description);
            Assert.Equal(trigger.Id, logEntry.EntityId);
            Assert.Equal(skill.Id, logEntry.SkillId);
            Assert.Equal(trigger.Description, logEntry.TriggerDescription);
            Assert.Equal(trigger.CronSchedule, logEntry.CronSchedule);
            Assert.Equal(trigger.TimeZoneId, logEntry.TimeZoneId);
            Assert.Equal("The Room", logEntry.Room);
        }
    }

    public class TheUpdateTriggerDescriptionAsyncMethod
    {
        [Fact]
        public async Task ChangesTriggerDescriptionAndLogsIt()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var skill = await env.CreateSkillAsync("test-skill");
            var triggerRepository = env.Activate<TriggerRepository>();
            var trigger = await triggerRepository.CreateHttpTriggerAsync(
                skill,
                "args",
                new PlatformRoom("the-cool-room-id", "The Room"),
                member);

            await triggerRepository.UpdateTriggerDescriptionAsync(trigger, "new description", member.User);

            Assert.Equal("new description", trigger.Description);
            var logEntry = await env.Db.AuditEvents.OfType<HttpTriggerChangeEvent>().LastAsync();
            Assert.Equal("Changed HTTP trigger `The Room` for skill `test-skill`.", logEntry.Description);
            Assert.Equal("The Room", logEntry.Room);
        }
    }

    public class TheDeleteTriggerAsyncMethod
    {
        [Fact]
        public async Task DeletesTriggerAndLogsIt()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var skill = await env.CreateSkillAsync("test-skill");
            var triggerRepository = env.Activate<TriggerRepository>();
            var trigger = await triggerRepository.CreateScheduledTriggerAsync(
                skill,
                "args",
                "A webhook",
                "0 0 * * *",
                new PlatformRoom("the-cool-room-id", "The Room"),
                member);

            await triggerRepository.DeleteTriggerAsync(trigger, member.User);

            var triggers = await env.Db.SkillScheduledTriggers.ToListAsync();
            Assert.Empty(triggers);
            var logEntry = await env.Db.AuditEvents.OfType<ScheduledTriggerChangeEvent>().LastAsync();
            Assert.Equal("Removed scheduled trigger `The Room` with schedule `At 12:00 AM, every day` for skill `test-skill`.", logEntry.Description);
            Assert.Equal("The Room", logEntry.Room);
        }
    }

    public class TheGetPlaybookFromTriggerTokenAsyncMethod
    {
        [Theory]
        [InlineData("U3lzdGVtLkJ5dGVbXTo0Mg")]
        [InlineData("U3lzdGVtLkJ5dGVbXToX")] // Token is case-sensitive
        [InlineData("n")] // Invalid format.
        public async Task ReturnsNullForPlaybookTokenMismatch(string apiToken)
        {
            var env = TestEnvironment.Create();

            await env.CreatePlaybookAsync(slug: "right", webhookTriggerTokenSeed: "y");

            using var _ = env.ActivateInNewScope<TriggerRepository>(out var isolated);

            var result = await isolated.GetPlaybookFromTriggerTokenAsync("right", apiToken);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsPlaybookWithTokenMatch()
        {
            var env = TestEnvironment.Create();

            var playbook = await env.CreatePlaybookAsync(slug: "right", webhookTriggerTokenSeed: "ToKeN");

            using var _ = env.ActivateInNewScope<TriggerRepository>(out var isolated);

            var result = await isolated.GetPlaybookFromTriggerTokenAsync("right", playbook.GetWebhookTriggerToken());

            Assert.NotNull(result);

            Assert.Equal("right", result.Slug);
            // Make sure our token is stable
            Assert.Equal("U3lzdGVtLkJ5dGVbXTox", playbook.GetWebhookTriggerToken());
        }
    }
}
