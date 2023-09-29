using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.PayloadHandlers;

public class SelectedRoomsAnnouncementTargetTests
{
    public class CustomerSegmentsAnnouncementTargetTests
    {
        public class TheGetAnnouncementRoomsAsyncMethod
        {
            [Fact]
            public async Task RetrievesAllSelectedRooms()
            {
                var env = TestEnvironment.Create();
                var organization = env.TestData.Organization;
                var sourceRoom = await env.CreateRoomAsync();
                var expectedRooms = new[]
                {
                    await env.CreateRoomAsync(managedConversationsEnabled: true),
                    await env.CreateRoomAsync(managedConversationsEnabled: true),
                };
                await env.CreateRoomAsync(managedConversationsEnabled: true);
                await env.CreateRoomAsync(managedConversationsEnabled: true);

                var announcement = new Announcement
                {
                    SourceRoom = sourceRoom,
                    Creator = env.TestData.User,
                    Organization = env.TestData.Organization,
                    SourceMessageId = "1234567.32434",
                };
                await env.Db.Announcements.AddAsync(announcement);
                await env.Db.SaveChangesAsync();
                announcement.Messages.AddRange(expectedRooms.Select(room => new AnnouncementMessage
                {
                    Room = room,
                    RoomId = room.Id,
                    Announcement = announcement,
                    AnnouncementId = organization.Id
                }));
                await env.Db.SaveChangesAsync();
                var target = env.Activate<SelectedRoomsAnnouncementTarget>();

                var result = await target.ResolveAnnouncementRoomsAsync(announcement);

                var roomIds = result.OrderBy(r => r.Room.Id).Select(r => r.Room.Id).ToArray();
                var expectedRoomIds = expectedRooms.Select(r => r.Id).ToArray();
                Assert.Equal(expectedRoomIds, roomIds);
            }
        }
    }
}
