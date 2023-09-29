using System.Linq;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Collections;
using Serious.Tasks;

namespace Serious.Abbot.Pages.Announcements;

public class IndexPage : UserPage
{
    const int PageSize = 10;

    readonly IAnnouncementsRepository _repository;
    readonly IAnnouncementCache _announcementCache;
    readonly IMessageRenderer _messageRenderer;

    public IPaginatedList<AnnouncementModel> UpcomingAnnouncements { get; set; } = null!;

    public IPaginatedList<AnnouncementModel> RecentlySentAnnouncements { get; set; } = null!;

    public bool ShowArchive { get; private set; }

    public IndexPage(
        IAnnouncementsRepository repository,
        IAnnouncementCache announcementCache,
        IMessageRenderer messageRenderer,
        IClock clock)
    {
        _repository = repository;
        _announcementCache = announcementCache;
        _messageRenderer = messageRenderer;
    }

    public async Task OnGetAsync(int? p = 1, int? ps = 1, bool archived = false)
    {
        int upcomingPage = p ?? 1;
        int recentlySentPage = ps ?? 1;

        int? recentlySentDays = 7;
        if (archived)
        {
            ShowArchive = true;
            recentlySentDays = null;
        }
        else
        {
            var upcomingAnnouncementsAsync = await _repository.GetUncompletedAnnouncementsAsync(
                upcomingPage,
                PageSize,
                Organization);

            var upcomingAnnouncementModels = await upcomingAnnouncementsAsync
                .SelectFunc(ToModelAsync)
                .WhenAllOneAtATimeAsync();

            UpcomingAnnouncements = PaginatedList.Create(
                upcomingAnnouncementModels,
                upcomingAnnouncementsAsync.PageNumber,
                upcomingAnnouncementsAsync.PageSize);
        }

        var sentAnnouncements = await _repository.GetCompletedAnnouncementsAsync(
            recentlySentPage,
            PageSize,
            recentlySentDays,
            Organization);

        var recentlySentAnnouncementModels = await sentAnnouncements
            .SelectFunc(ToModelAsync)
            .WhenAllOneAtATimeAsync();
        RecentlySentAnnouncements = PaginatedList.Create(
            recentlySentAnnouncementModels,
            sentAnnouncements.PageNumber,
            sentAnnouncements.PageSize,
            pageQueryStringParameterName: "ps");
    }

    async Task<AnnouncementModel?> ToModelAsync(Announcement announcement)
    {
        var announcementText = await GetTextAsync(announcement);
        var renderedMessage = await _messageRenderer.RenderMessageAsync(announcementText, announcement.Organization);

        return AnnouncementModel.FromAnnouncement(announcement, renderedMessage);
    }

    async Task<string?> GetTextAsync(Announcement announcement)
    {
        return await _announcementCache.GetAndCacheAnnouncementTextAsync(announcement);
    }
}

public record AnnouncementModel(
    int Id,
    RenderedMessage? RenderedMessage,
    Announcement Announcement,
    Room SourceRoom,
    Member? Author,
    AnnouncementState State,
    int ErrorCount)
{
    public static AnnouncementModel FromAnnouncement(Announcement announcement, RenderedMessage? renderedMessage)
    {
        var announcementState = announcement switch
        {
            { DateCompletedUtc: not null } => AnnouncementState.Completed,
            { DateStartedUtc: not null } => AnnouncementState.InProgress,
            _ => AnnouncementState.Scheduled
        };

        var errorCount = announcement.Messages.Count(m => m.ErrorMessage is { Length: > 0 });

        // Yes, this is hacky. https://github.com/aseriousbiz/abbot/issues/2628 is tracking what we should do here.
        var author = announcement
            .Creator
            .Members
            .SingleOrDefault(m => m.OrganizationId == announcement.OrganizationId);

        return new AnnouncementModel(
            announcement.Id,
            renderedMessage,
            announcement,
            announcement.SourceRoom,
            author,
            announcementState,
            errorCount);
    }
}

public enum AnnouncementState
{
    Scheduled,
    InProgress,
    Completed,
}
