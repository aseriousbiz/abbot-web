using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;

public class SignalRepositoryTests
{
    public class TheGetAllAsyncMethod
    {
        [Fact]
        public async Task RetrievesSignalsForEnabledNotDeletedSkills()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var skill1 = await env.CreateSkillAsync("test-skill1");
            skill1.SignalSubscriptions = new List<SignalSubscription>
            {
                new() { Name = "signal-1" },
                new() { Name = "signal-2" }
            };
            var skill2 = await env.CreateSkillAsync("test-skill2");
            skill2.Enabled = false;
            skill2.SignalSubscriptions = new List<SignalSubscription>
            {
                new() { Name = "signal-3" }
            };
            var skill3 = await env.CreateSkillAsync("test-skill3");
            skill3.IsDeleted = true;
            skill3.SignalSubscriptions = new List<SignalSubscription>
            {
                new() { Name = "signal-4" }
            };
            var skill4 = await env.CreateSkillAsync("test-skill4", org: env.TestData.ForeignOrganization);
            skill4.SignalSubscriptions = new List<SignalSubscription>
            {
                new() { Name = "signal-3" },
                new() { Name = "signal-5" }
            };
            var skill5 = await env.CreateSkillAsync("test-skill5");
            skill5.SignalSubscriptions = new List<SignalSubscription>
            {
                new() { Name = "signal-2" },
                new() { Name = "signal-6" }
            };
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<SignalRepository>();

            var signals = (await repository.GetAllAsync(organization))
                .OrderBy(s => s).ToList();

            Assert.Equal(3, signals.Count);
            Assert.Equal("signal-1", signals[0]);
            Assert.Equal("signal-2", signals[1]);
            Assert.Equal("signal-6", signals[2]);
        }
    }

    public class TheGetAsyncMethod
    {
        [Fact]
        public async Task GetsSignalSubscriptionByName()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("signal-subscriber");
            var repository = env.Activate<SignalRepository>();
            await repository.CreateAsync(
                "A-Cool-Signal",
                null,
                PatternType.None,
                false,
                skill,
                env.TestData.User);

            var retrieved = await repository.GetAsync(
                "a-cool-SIGNAL",
                "signal-subscriber",
                env.TestData.Organization);

            Assert.NotNull(retrieved);
            Assert.Equal("a-cool-signal", retrieved.Name);
        }
    }
}
