using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Xunit;

public class ListRepositoryTests
{
    public class TheGetAsyncMethod
    {
        [Fact]
        public async Task RetrievesCreatorAndEntries()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var list = new UserList
            {
                Name = "cool",
                Organization = organization,
                Entries = new List<UserListEntry>
                {
                    new()
                    {
                        Content = "content",
                        Creator = user,
                        ModifiedBy = user
                    }
                }
            };
            var repository = env.Activate<ListRepository>();
            await repository.CreateAsync(list, user);

            var retrieved = await repository.GetAsync("cool", organization);

            Assert.NotNull(retrieved);
            Assert.NotNull(retrieved.Creator);
            Assert.Single(retrieved.Entries);
        }
    }

    public class TheAddEntryToListMethod
    {
        [Fact]
        public async Task LogsAndAddsEntryToList()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var list = new UserList
            {
                Name = "cool",
                Organization = organization
            };
            var repository = env.Activate<ListRepository>();
            await repository.CreateAsync(list, user);

            var entry = await repository.AddEntryToList(list, "some-content", user);

            Assert.Equal("some-content", entry.Content);
            var logs = await env.AuditLog.GetAuditEventsAsync(
                organization,
                1,
                10,
                StatusFilter.Success,
                ActivityTypeFilter.All,
                null,
                null,
                true);
            Assert.Equal("Added `some-content` to list `cool`.", logs.First().Description);
        }
    }

    public class TheRemoveEntryFromListMethod
    {
        [Fact]
        public async Task LogsAndAddsEntryToList()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var list = new UserList
            {
                Name = "cool",
                Organization = organization
            };
            var repository = env.Activate<ListRepository>();
            await repository.CreateAsync(list, user);
            await repository.AddEntryToList(list, "some-content", user);

            var removed = await repository.RemovesEntryFromList(list, "some-content", user);

            Assert.True(removed);
            var logs = await env.AuditLog.GetAuditEventsAsync(
                organization,
                1,
                10,
                StatusFilter.Success,
                ActivityTypeFilter.All,
                null,
                null,
                true);
            Assert.Equal("Removed `some-content` from list `cool`.", logs.First().Description);
        }
    }
}
