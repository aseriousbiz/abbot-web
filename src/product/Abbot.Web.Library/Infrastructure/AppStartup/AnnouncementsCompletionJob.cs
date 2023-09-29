using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.AppStartup;

public class AnnouncementsCompletionJob : IRecurringJob
{
    readonly AbbotContext _db;

    public AnnouncementsCompletionJob(AbbotContext db)
    {
        _db = db;
    }

    public static string Name => "Update Completed Announcements";

    [Queue(HangfireQueueNames.NormalPriority)]
    [AutomaticRetry(Attempts = 0)] // We don't want this job to retry. It'll run again on its next scheduled time.
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // These are all announcements that should be completed, but aren't for whatever reason.
        var completedAnnouncements = await _db.Announcements
            .Include(a => a.Messages)
            .Where(a => a.DateCompletedUtc == null)
            .Where(a => a.Messages.All(m => m.SentDateUtc != null))
            .ToListAsync(cancellationToken);

        foreach (var completedAnnouncement in completedAnnouncements)
        {
            completedAnnouncement.DateCompletedUtc = completedAnnouncement.Messages.Max(m => m.SentDateUtc);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
