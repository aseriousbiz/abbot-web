using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Xunit;

namespace Abbot.Web.Library.Tests.Repositories;

public class HubRepositoryTests
{
    public class TheGetHubAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullIfNoHubMatchesRoom()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();

            var repository = env.Activate<HubRepository>();

            Assert.Null(await repository.GetHubAsync(room));
        }

        [Fact]
        public async Task ReturnsMatchingHubIfFound()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            env.Db.Hubs.Add(new Hub()
            {
                Name = "test-hub",
                Room = room,
                RoomId = room.Id,
                Organization = env.TestData.Organization,
            });
            await env.Db.SaveChangesAsync();

            var repository = env.Activate<HubRepository>();

            var hub = await repository.GetHubAsync(room);
            Assert.NotNull(hub);
            Assert.Equal("test-hub", hub.Name);
        }
    }

    public class TheGetAllHubsAsyncMethod
    {
        [Fact]
        public async Task ReturnsEmptyListIfNoHubsInOrganization()
        {
            var env = TestEnvironment.Create();
            var otherRoom = await env.CreateRoomAsync(org: env.TestData.ForeignOrganization);
            var repository = env.Activate<HubRepository>();
            var otherHub = await repository.CreateHubAsync("other-hub", otherRoom, env.TestData.ForeignMember);
            Assert.Empty(await repository.GetAllHubsAsync(env.TestData.Organization));
        }

        [Fact]
        public async Task ReturnsAllHubsInOrganization()
        {
            var env = TestEnvironment.Create();
            var room1 = await env.CreateRoomAsync();
            var room2 = await env.CreateRoomAsync();

            var repository = env.Activate<HubRepository>();
            await repository.CreateHubAsync("hub-1", room1, env.TestData.Member);
            await repository.CreateHubAsync("hub-2", room2, env.TestData.Member);

            var list = await repository.GetAllHubsAsync(env.TestData.Organization);

            Assert.Equal(new[] { "hub-1", "hub-2" }, list.Select(h => h.Name).ToArray());
        }
    }

    public class TheCreateHubAsyncMethod
    {
        [Fact]
        public async Task CreatesANewHub()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<HubRepository>();

            var hub = await repository.CreateHubAsync("test-hub", room, env.TestData.Member);
            var stored = await env.Db.Hubs.SingleAsync();

            Assert.Equal(hub.Id, stored.Id);
            Assert.Equal("test-hub", stored.Name);
            Assert.Equal(room, stored.Room);
            Assert.Equal(env.Clock.UtcNow, stored.Created);

            var auditEvent = await env.AuditLog.GetMostRecentLogEntry(env.TestData.Organization);
            var hubAuditEvent = Assert.IsType<HubAuditEvent>(auditEvent);
            Assert.Equal(hub.Id, hubAuditEvent.EntityId);
            Assert.Equal(env.TestData.User.Id, hubAuditEvent.ActorId);
            Assert.Equal($"Created hub `test-hub` in `#{room.Name}`", hubAuditEvent.Description);
            Assert.Equal(room.Name, hubAuditEvent.Room);
            Assert.Equal(room.PlatformRoomId, hubAuditEvent.RoomId);
        }
    }

    public class TheDeleteHubAsyncMethod
    {
        [Fact]
        public async Task DeletesTheSpecifiedHub()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<HubRepository>();
            var hub = await repository.CreateHubAsync("test-hub", room, env.TestData.Member);

            await repository.DeleteHubAsync(hub, env.TestData.Member);
            Assert.Empty(env.Db.Hubs);

            var auditEvent = await env.AuditLog.GetMostRecentLogEntry(env.TestData.Organization);
            var hubAuditEvent = Assert.IsType<HubAuditEvent>(auditEvent);
            Assert.Equal(hub.Id, hubAuditEvent.EntityId);
            Assert.Equal(env.TestData.User.Id, hubAuditEvent.ActorId);
            Assert.Equal($"Deleted hub `test-hub`", hubAuditEvent.Description);
            Assert.Equal(room.Name, hubAuditEvent.Room);
            Assert.Equal(room.PlatformRoomId, hubAuditEvent.RoomId);
        }
    }

    public class TheGetAttachedRoomsAsyncMethod
    {
        [Fact]
        public async Task ReturnsAttachedRooms()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var hubRoom = await env.CreateRoomAsync("Chub-room");
            var hub = await env.Hubs.CreateHubAsync("The Hub", hubRoom, env.TestData.Member);

            var attachedRoom1 = await env.CreateRoomAsync("Cattached-room-1");
            await env.CreateRoomAsync("Cnot-attached-room");
            var attachedRoom2 = await env.CreateRoomAsync("Cattached-room-2");
            await env.Rooms.AttachToHubAsync(attachedRoom1, hub, env.TestData.Member);
            await env.Rooms.AttachToHubAsync(attachedRoom2, hub, env.TestData.Member);
            var repository = env.Activate<HubRepository>();

            var attachedRooms = await repository.GetAttachedRoomsAsync(hub);
            Assert.Equal(new[] { attachedRoom1, attachedRoom2 }, attachedRooms);
        }
    }
}
