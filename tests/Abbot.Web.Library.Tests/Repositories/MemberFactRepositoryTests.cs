using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Xunit;

public class MemberFactRepositoryTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesFactAndLogsIt()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var subject = await env.CreateMemberAsync();
            var fact = new MemberFact
            {
                Subject = subject,
                Content = "is so hot right now"
            };
            var repository = env.Activate<MemberFactRepository>();

            await repository.CreateAsync(fact, user);

            var facts = await repository.GetFactsAsync(subject);
            var retrieved = Assert.Single(facts);
            Assert.NotNull(retrieved);
            Assert.Equal("is so hot right now", retrieved.Content);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal($"Created fact `is so hot right now` for the user `{subject.DisplayName}` via the `who` skill.",
                log.Description);
        }
    }

    public class TheRemoveAsyncMethod
    {
        [Fact]
        public async Task RemovesFactAndLogsIt()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var subject = await env.CreateMemberAsync();
            var fact = new MemberFact
            {
                Subject = subject,
                Content = "is so hot right now"
            };
            var repository = env.Activate<MemberFactRepository>();
            await repository.CreateAsync(fact, user);

            await repository.RemoveAsync(fact, user);

            var facts = await repository.GetFactsAsync(subject);
            Assert.Empty(facts);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal($"Removed fact `is so hot right now` from the user `{subject.DisplayName}` via the `who` skill.",
                log.Description);
        }
    }
}
