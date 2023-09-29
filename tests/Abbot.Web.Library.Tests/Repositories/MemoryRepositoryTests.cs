using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Xunit;

public class MemoryRepositoryTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesAMemory()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var memory = new Memory
            {
                Name = "nature-of-memory",
                Content = "Memories are ephemeral",
                Organization = organization
            };
            var repository = env.Activate<MemoryRepository>();

            await repository.CreateAsync(memory, user);

            var result = await repository.GetAsync("nature-of-memory", organization);
            Assert.NotNull(result);
            Assert.Equal(user, result.Creator);
            Assert.Equal("nature-of-memory", result.Name);
            Assert.Equal("Memories are ephemeral", result.Content);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal("Added memory `nature-of-memory` via the `rem` skill.", log.Description);
        }
    }

    public class TheRemoveAsyncMethod
    {
        [Fact]
        public async Task RemovesAMemory()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;

            var memory = new Memory
            {
                Name = "nature-of-memory",
                Content = "Memories are ephemeral",
                Organization = organization
            };
            var repository = env.Activate<MemoryRepository>();
            var created = await repository.CreateAsync(memory, user);

            await repository.RemoveAsync(created, user);

            var result = await repository.GetAsync("nature-of-memory", organization);
            Assert.Null(result);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal("Removed memory `nature-of-memory` via the `forget` skill.", log.Description);
        }
    }

    public class TheGetAllAsyncMethod
    {
        [Fact]
        public async Task IncludesCreator()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var memory = new Memory
            {
                Name = "nature-of-memory",
                Content = "Memories are ephemeral",
                Organization = organization
            };
            var repository = env.Activate<MemoryRepository>();
            await repository.CreateAsync(memory, user);

            var result = await repository.GetAllAsync(organization);

            Assert.NotNull(result[0].Creator);
        }
    }
}
