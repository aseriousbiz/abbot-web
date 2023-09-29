using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.PayloadHandlers;

public class CustomerSegmentsAnnouncementTargetTests
{
    public class TheGetAnnouncementRoomsAsyncMethod
    {
        [Fact]
        public async Task RetrievesAllRoomsForCustomerSegments()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            var sourceRoom = await env.CreateRoomAsync();
            var customerSegments = await env.Customers.GetOrCreateSegmentsByNamesAsync(
                    new[] { "Enterprise", "SMB", "Scooters" },
                    actor,
                    env.TestData.Organization);
            var room1 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var room2 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var room3 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var room4 = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var customer1 = await env.Customers.CreateCustomerAsync(
                "Initech",
                new[] { room1 },
                actor,
                organization,
                null);
            var customer2 = await env.Customers.CreateCustomerAsync(
                "Dunder Mifflin",
                new[] { room2 },
                actor,
                organization,
                null);
            var customer3 = await env.Customers.CreateCustomerAsync(
                "ACME",
                new[] { room3, room4 },
                actor,
                organization,
                null);
            await env.Customers.AssignCustomerToSegmentsAsync(
                customer1,
                new Id<CustomerTag>[] { customerSegments[0] },
                actor);
            await env.Customers.AssignCustomerToSegmentsAsync(
                customer2,
                new Id<CustomerTag>[] { customerSegments[0] },
                actor);
            await env.Customers.AssignCustomerToSegmentsAsync(
                customer2,
                new Id<CustomerTag>[] { customerSegments[1] },
                actor);
            await env.Customers.AssignCustomerToSegmentsAsync(
                customer3,
                new Id<CustomerTag>[] { customerSegments[2] },
                actor);
            var announcement = new Announcement
            {
                SourceRoom = sourceRoom,
                Creator = env.TestData.User,
                Organization = env.TestData.Organization,
                SourceMessageId = "1234567.32434",
            };
            await env.Db.Announcements.AddAsync(announcement);
            await env.Db.SaveChangesAsync();
            announcement.CustomerSegments.AddRange(customerSegments
                .Where(s => s.Name is "Enterprise" or "SMB")
                .Select(s => new AnnouncementCustomerSegment
                {
                    AnnouncementId = announcement.Id,
                    Announcement = announcement,
                    CustomerTagId = s.Id,
                    CustomerTag = s,
                }));
            await env.Db.SaveChangesAsync();
            var target = env.Activate<CustomerSegmentsAnnouncementTarget>();

            var result = await target.ResolveAnnouncementRoomsAsync(announcement);

            var roomIds = result.OrderBy(r => r.Room.Id).Select(r => r.Room.Id).ToArray();
            var expectedRoomIds = new[] { room1.Id, room2.Id };
            Assert.Equal(expectedRoomIds, roomIds);
        }
    }
}
