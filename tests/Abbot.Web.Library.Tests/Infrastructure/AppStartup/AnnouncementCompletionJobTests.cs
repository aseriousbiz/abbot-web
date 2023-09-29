using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Infrastructure.AppStartup;
using Xunit;

public class AnnouncementCompletionJobTests
{
    [Fact]
    public async Task CompletesSentAnnouncements()
    {
        var env = TestEnvironment.Create();
        var sourceRoom = await env.CreateRoomAsync();
        var targetRoom1 = await env.CreateRoomAsync();
        var targetRoom2 = await env.CreateRoomAsync();
        var targetRoom3 = await env.CreateRoomAsync();
        var announcement = await env.CreateAnnouncementAsync(
            sourceRoom,
            "Message Id",
            null,
            targetRoom1,
            targetRoom2,
            targetRoom3);

        foreach (var message in announcement.Messages)
        {
            message.SentDateUtc = env.Clock.UtcNow;
        }
        await env.Db.SaveChangesAsync();
        var job = env.Activate<AnnouncementsCompletionJob>();

        await job.RunAsync(default);

        Assert.NotNull(announcement.DateCompletedUtc);
    }

    [Fact]
    public async Task DoesNotCompletesIncompleteAnnouncements()
    {
        var env = TestEnvironment.Create();
        var sourceRoom = await env.CreateRoomAsync();
        var targetRoom1 = await env.CreateRoomAsync();
        var targetRoom2 = await env.CreateRoomAsync();
        var targetRoom3 = await env.CreateRoomAsync();
        var announcement = await env.CreateAnnouncementAsync(
            sourceRoom,
            "Message Id",
            null,
            targetRoom1,
            targetRoom2,
            targetRoom3);
        announcement.Messages[0].SentDateUtc = env.Clock.UtcNow;
        announcement.Messages[1].SentDateUtc = env.Clock.UtcNow;
        await env.Db.SaveChangesAsync();
        var job = env.Activate<AnnouncementsCompletionJob>();

        await job.RunAsync(default);

        Assert.Null(announcement.DateCompletedUtc);
    }
}
