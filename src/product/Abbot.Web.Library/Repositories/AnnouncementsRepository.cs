using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using Serious.Logging;

namespace Serious.Abbot.Repositories;

public class AnnouncementsRepository : OrganizationScopedRepository<Announcement>, IAnnouncementsRepository
{
    static readonly ILogger<AnnouncementsRepository> Log = ApplicationLoggerFactory.CreateLogger<AnnouncementsRepository>();
    readonly IClock _clock;

    public AnnouncementsRepository(AbbotContext db, IClock clock, IAuditLog auditLog) : base(db, auditLog)
    {
        _clock = clock;
    }

    public async Task<IPaginatedList<Announcement>> GetCompletedAnnouncementsAsync(
        int page,
        int pageSize,
        int? pastNumberOfDays,
        Organization organization)
    {
        var queryable = GetQueryable(organization);

        queryable = pastNumberOfDays.HasValue
            ? queryable.Where(a => a.DateCompletedUtc > _clock.UtcNow.AddDays(-1 * pastNumberOfDays.Value))
            : queryable.Where(a => a.DateCompletedUtc != null);

        queryable = queryable.OrderByDescending(a => a.DateCompletedUtc);
        return await PaginatedList.CreateAsync(queryable, page, pageSize);
    }

    public async Task<IPaginatedList<Announcement>> GetUncompletedAnnouncementsAsync(
        int page,
        int pageSize,
        Organization organization)
    {
        var queryable = GetQueryable(organization)
            .Where(a => a.DateCompletedUtc == null)
            .OrderByDescending(a => a.DateStartedUtc ?? a.ScheduledDateUtc ?? a.Created);
        return await PaginatedList.CreateAsync(queryable, page, pageSize);
    }

    public async Task<Announcement?> GetAnnouncementFromMessageAsync(
        string platformRoomId,
        string messageId,
        Organization organization)
    {
        return await GetQueryable(organization)
            .SingleOrDefaultAsync(a => a.SourceMessageId == messageId
                && a.SourceRoom.PlatformRoomId == platformRoomId);
    }

    public async Task<Announcement> RequireForSchedulingAsync(int id)
    {
        return await GetEntitiesQueryable()
                   .SingleOrDefaultAsync(a => a.Id == id)
                   .Require($"Announcement not found for Id {id}");
    }

    public async Task<AnnouncementMessage> RequireMessageForSendingAsync(int id)
    {
        return await Db.AnnouncementMessages
            .Include(m => m.Announcement.CustomerSegments)
            .ThenInclude(cs => cs.CustomerTag)
            .Include(m => m.Announcement.Creator)
            .Include(m => m.Announcement.Organization)
            .Include(m => m.Announcement.Messages)  // We want them all so we know when the announcement is complete.
            .ThenInclude(m => m.Room)
            .Where(m => m.Announcement.IsDeleted == false)
            .SingleOrDefaultAsync(m => m.Id == id)
            .Require($"AnnouncementMessage not found for Id {id}");
    }

    public async Task UpdateAnnouncementTextAndStartDateAsync(Announcement announcement, string text, DateTime utcNow)
    {
        announcement.Text = text;
        announcement.DateStartedUtc = utcNow;
        await Db.SaveChangesAsync();
        Log.AnnouncementStarting(announcement.Id, announcement.DateStartedUtc, announcement.ScheduledDateUtc);
    }

    public async Task UpdateMessageSendCompletedAsync(
        AnnouncementMessage message,
        string? messageId,
        string? errorMessage,
        DateTime utcNow)
    {
        // The messageId might be null when updating a message that failed to send.
        // We want to be able to update it without overwriting the original messageId.
        if (messageId is not null)
        {
            message.MessageId = messageId;
        }

        message.SentDateUtc = utcNow;
        message.ErrorMessage = errorMessage;
        await Db.SaveChangesAsync();
    }

    public async Task<bool> SetAnnouncementCompletedAsync(Id<Announcement> announcementId)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync();
        var announcement = await RequireForSchedulingAsync(announcementId);
        if (announcement.DateCompletedUtc.HasValue)
        {
            Log.AnnouncementAlreadyCompleted(announcementId, announcement.DateCompletedUtc);
            return false;
        }

        if (announcement.Messages.All(m => m.SentDateUtc.HasValue))
        {
            announcement.DateCompletedUtc = _clock.UtcNow;
            await Db.SaveChangesAsync();
            await transaction.CommitAsync();
            Log.AnnouncementCompleted(announcement.Id, announcement.DateCompletedUtc);
            return true;
        }

        return false;
    }

    protected override DbSet<Announcement> Entities => Db.Announcements;

    protected override IQueryable<Announcement> GetEntitiesQueryable()
    {
        return Entities
            .Include(a => a.CustomerSegments)
            .ThenInclude(cs => cs.CustomerTag)
            .Include(a => a.Creator)
            .Include(a => a.Creator.Members)
            .Include(a => a.SourceRoom)
            .Include(a => a.Organization)
            .Include(a => a.Messages)
            .ThenInclude(a => a.Room);
    }
}

public static partial class AnnouncementsRepositoryLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message =
            "Setting Announcement {AnnouncementId} start date to {StartDate} (It was scheduled to start at {ScheduledStartDate}).")]
    public static partial void AnnouncementStarting(this ILogger<AnnouncementsRepository> logger,
        int announcementId, DateTime? startDate, DateTime? scheduledStartDate);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Setting Announcement {AnnouncementId} completed on {CompletedDate}.")]
    public static partial void AnnouncementCompleted(this ILogger<AnnouncementsRepository> logger,
        int announcementId, DateTime? completedDate);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Announcement {AnnouncementId} ALREADY completed on {CompletedDate}.")]
    public static partial void AnnouncementAlreadyCompleted(this ILogger<AnnouncementsRepository> logger,
        int announcementId, DateTime? completedDate);
}
