using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Humanizer;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Logging;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

public class SelectedRoomsAnnouncementTarget : IAnnouncementTarget
{
    readonly ISlackResolver _resolver;
    static readonly ILogger<SelectedRoomsAnnouncementTarget> Log = ApplicationLoggerFactory.CreateLogger<SelectedRoomsAnnouncementTarget>();

    public SelectedRoomsAnnouncementTarget(ISlackResolver resolver)
    {
        _resolver = resolver;
    }

    public string GetSuccessMessageLabel(Announcement announcement) => $"in channels {announcement.Messages.ToRoomMentionList()}";

    public Task<IReadOnlyList<AnnouncementMessage>> ResolveAnnouncementRoomsAsync(Announcement announcement)
    {
        Log.AnnouncingToSpecifiedRooms(announcement.Id, announcement.Messages.Count);
        return Task.FromResult(announcement.Messages.ToReadOnlyList());
    }

    public bool IsTargetForAnnouncement(Announcement announcement)
        => announcement.Messages.Count > 0 && announcement.CustomerSegments.Count == 0;

    public bool IsSelectedTarget(string? targetName) => targetName is nameof(SelectedRoomsAnnouncementTarget)
        // We can remove the "or" clause after this has been deployed a while.
        or "channels-pick";

    public async Task<bool> HandleTargetSelectedAsync(
        IViewContext<IViewSubmissionPayload> viewContext,
        BlockActionsState state,
        Announcement announcement)
    {
        var channelsMenu = state.GetAs<MultiExternalSelectMenu>(AnnouncementHandler.Blocks.Channels, AnnouncementHandler.ActionIds.Channels);
        var selectedValues = channelsMenu.SelectedOptions.Select(o => o.Value).ToList();

        Log.SelectedRooms(string.Join(", ", selectedValues));

        var rooms = await _resolver.ResolveRoomsAsync(
            selectedValues,
            viewContext.Organization,
            true);

        var selectedRooms = rooms
            .WhereNotNull()
            .Select(room => new AnnouncementMessage
            {
                Room = room
            }).ToList();

        if (selectedRooms.Count is 0)
        {
            viewContext.ReportValidationErrors(AnnouncementHandler.Blocks.Channels, "Some of the channels cannot be messaged.");
            return false;
        }

        var roomsWithoutAbbot = selectedRooms.Where(r => r.Room.BotIsMember is false).ToList();
        if (roomsWithoutAbbot.Count > 0)
        {
            var roomNames = roomsWithoutAbbot.Select(r => r.Room.Name ?? r.Room.PlatformRoomId).Humanize();
            viewContext.ReportValidationErrors(
                AnnouncementHandler.Blocks.Channels,
                "In order to send an announcement to a room, Abbot must be a member of that room: Abbot is "
                + $"not a member of the following rooms: {roomNames}.");
            return false;
        }

        announcement.Messages.Sync(selectedRooms, msg => msg.Room.Id);
        return true;
    }
}

public static partial class SelectedRoomsAnnouncementTargetLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Broadcasting Announcement {AnnouncementId} to {RoomCount} specified rooms.")]
    public static partial void AnnouncingToSpecifiedRooms(
        this ILogger<SelectedRoomsAnnouncementTarget> logger,
        int announcementId,
        int roomCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Selected Rooms: \"{selectedRooms}\"")]
    public static partial void SelectedRooms(
        this ILogger<SelectedRoomsAnnouncementTarget> logger,
        string? selectedRooms);

}
