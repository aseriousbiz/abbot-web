using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Filters;
using Serious.Logging;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

public class AllRoomsAnnouncementTarget : IAnnouncementTarget
{
    static readonly ILogger<AllRoomsAnnouncementTarget> Log = ApplicationLoggerFactory.CreateLogger<AllRoomsAnnouncementTarget>();

    readonly IRoomRepository _roomRepository;

    public AllRoomsAnnouncementTarget(IRoomRepository roomRepository)
    {
        _roomRepository = roomRepository;
    }

    public string GetSuccessMessageLabel(Announcement announcement) => "in all external channels";

    public async Task<IReadOnlyList<AnnouncementMessage>> ResolveAnnouncementRoomsAsync(Announcement announcement)
    {
        var trackedRooms = await _roomRepository.GetPersistentRoomsAsync(
            announcement.Organization,
            new FilterList(), // Empty filter.
            TrackStateFilter.Tracked,
            page: 1,
            pageSize: int.MaxValue);

        var messages = trackedRooms
            .Where(r => r.Shared is true)
            .Select(room => new AnnouncementMessage
            {
                Room = room
            }).ToReadOnlyList();

        Log.AnnouncingToTrackedSharedRooms(announcement.Id, messages.Count);
        return messages;
    }

    public bool IsTargetForAnnouncement(Announcement announcement)
    {
        return announcement.Messages.Count == 0 && announcement.CustomerSegments.Count == 0;
    }

    public bool IsSelectedTarget(string? targetName) => targetName is nameof(AllRoomsAnnouncementTarget)
        // We can remove the "or" clause after this has been deployed a while.
        or "channels-all-external";

    public async Task<bool> HandleTargetSelectedAsync(
        IViewContext<IViewSubmissionPayload> viewContext,
        BlockActionsState state,
        Announcement announcement)
    {
        announcement.Messages.Clear();
        Log.SelectedRooms("(All Tracked External Rooms)");
        return true;
    }
}

public static partial class AllRoomsAnnouncementDispatcherLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Broadcasting Announcement {AnnouncementId} to {RoomCount} tracked shared rooms.")]
    public static partial void AnnouncingToTrackedSharedRooms(
        this ILogger<AllRoomsAnnouncementTarget> logger,
        int announcementId,
        int roomCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Selected Rooms: \"{selectedRooms}\"")]
    public static partial void SelectedRooms(
        this ILogger<AllRoomsAnnouncementTarget> logger,
        string? selectedRooms);
}
