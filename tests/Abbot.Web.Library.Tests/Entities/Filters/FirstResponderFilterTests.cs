using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Entities.Filters;
using Serious.Filters;

public class FirstResponderFilterTests
{
    public class TheApplyMethod
    {
        [Fact]
        public async Task FiltersRoomsBasedOnAssignedResponders()
        {
            var env = TestEnvironment.Create();
            var room1 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var room2 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var agent1 = await env.CreateMemberInAgentRoleAsync();
            var agent2 = await env.CreateMemberInAgentRoleAsync();
            await env.Rooms.AssignMemberAsync(room1, agent1, RoomRole.FirstResponder, env.TestData.Abbot);
            await env.Rooms.AssignMemberAsync(room1, agent2, RoomRole.FirstResponder, env.TestData.Abbot);
            await env.Rooms.AssignMemberAsync(room2, agent1, RoomRole.EscalationResponder, env.TestData.Abbot);
            var filter = new QueryFilter<Room>(RoomFilters.CreateFilters(env.Db));
            var query = env.Db.Rooms;

            var firstResponderFiltered = filter.Apply(
                query,
                new FilterList { Filter.Create("fr", agent1.User.PlatformUserId) });

            var result = await firstResponderFiltered.ToListAsync();
            var singleResult = Assert.Single(result);
            Assert.Equal(room1.Id, singleResult.Id);

            var escalationResponderFiltered = filter.Apply(
                query,
                new FilterList { Filter.Create("er", agent1.User.PlatformUserId) });

            var result2 = await escalationResponderFiltered.ToListAsync();
            var singleResult2 = Assert.Single(result2);
            Assert.Equal(room2.Id, singleResult2.Id);
        }

        [Fact]
        public async Task CanExcludeRoomsWithDefaultResponder()
        {
            var env = TestEnvironment.Create();
            var room1 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var room2 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var agent1 = await env.CreateMemberInAgentRoleAsync();
            var agent2 = await env.CreateMemberInAgentRoleAsync();
            await env.Rooms.AssignMemberAsync(room1, agent1, RoomRole.FirstResponder, env.TestData.Abbot);
            await env.Rooms.AssignMemberAsync(room1, agent2, RoomRole.FirstResponder, env.TestData.Abbot);
            await env.Rooms.AssignMemberAsync(room2, agent1, RoomRole.EscalationResponder, env.TestData.Abbot);
            var filter = new QueryFilter<Room>(RoomFilters.CreateFilters(env.Db));
            var query = env.Db.Rooms;

            var result = await filter.Apply(
                query,
                new FilterList { Filter.Create("-fr", agent1.User.PlatformUserId) }).ToListAsync();

            var singleResult = Assert.Single(result);
            Assert.Equal(room2.Id, singleResult.Id); // Agent 1 is not an fr for room2.
        }
    }
}
