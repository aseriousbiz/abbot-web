using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

public record CustomerSegmentsAnnouncementTarget : IAnnouncementTarget
{
    readonly CustomerRepository _customerRepository;
    readonly IRoomRepository _roomRepository;

    public CustomerSegmentsAnnouncementTarget(CustomerRepository customerRepository, IRoomRepository roomRepository)
    {
        _customerRepository = customerRepository;
        _roomRepository = roomRepository;
    }

    static readonly ILogger<CustomerSegmentsAnnouncementTarget> Log = ApplicationLoggerFactory.CreateLogger<CustomerSegmentsAnnouncementTarget>();

    public string GetSuccessMessageLabel(Announcement announcement)
        => $"to channels belonging to customer segments {GetCustomerSegmentNames(announcement).Humanize()}";

    public async Task<IReadOnlyList<AnnouncementMessage>> ResolveAnnouncementRoomsAsync(Announcement announcement)
    {
        var segmentIds = announcement.CustomerSegments.Select(s => new Id<CustomerTag>(s.CustomerTagId));
        var rooms = await _roomRepository.GetRoomsByCustomerSegmentsAsync(
            segmentIds,
            TrackStateFilter.Tracked,
            announcement.Organization);
        Log.AnnouncingToCustomerSegments(announcement, GetCustomerSegmentNames(announcement));
        return rooms.Select(room => new AnnouncementMessage
        {
            Room = room,
            RoomId = room.Id,
        }).ToReadOnlyList();
    }

    public bool IsTargetForAnnouncement(Announcement announcement) => announcement.CustomerSegments.Any();

    public bool IsSelectedTarget(string? targetName) => targetName is nameof(CustomerSegmentsAnnouncementTarget)
        // We can remove the "or" clause after this has been deployed a while.
        or "segments-pick";

    public async Task<bool> HandleTargetSelectedAsync(
        IViewContext<IViewSubmissionPayload> viewContext,
        BlockActionsState state,
        Announcement announcement)
    {
        var segmentsMenu = state.GetAs<MultiStaticSelectMenu>(
            AnnouncementHandler.Blocks.Segments,
            AnnouncementHandler.ActionIds.Segments);
        var customerSegments = segmentsMenu.SelectedOptions.Select(o => o.Text.Text).ToList();
        Log.SelectedSegments(string.Join(", ", customerSegments));
        var result = await _customerRepository.GetCustomerSegmentsByNamesAsync(
            customerSegments,
            viewContext.Organization);

        var missingSegments = result.Where(r => r.ErrorMessage is not null).Select(r => r.Key).ToList();
        if (missingSegments.Any())
        {
            viewContext.ReportValidationErrors(
                AnnouncementHandler.Blocks.Segments,
                $"The following customer segments do not exist or are not retrievable at this time: {missingSegments}.");
            return false;
        }

        var selectedSegments = result
            .Select(r => r.Entity)
            .WhereNotNull()
            .Select(segment => new AnnouncementCustomerSegment
            {
                CustomerTag = segment,
            }).ToList();
        announcement.CustomerSegments.Sync(selectedSegments, seg => seg.CustomerTag.Id);
        return true;
    }

    static string GetCustomerSegmentNames(Announcement announcement)
        => announcement.CustomerSegments.Select(s => s.CustomerTag.Name).Humanize();
}

public static partial class CustomerSegmentsAnnouncementTargetLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Broadcasting Announcement {AnnouncementId} to {CustomerSegments} customer segments.")]
    public static partial void AnnouncingToCustomerSegments(
        this ILogger<CustomerSegmentsAnnouncementTarget> logger,
        Id<Announcement> announcementId,
        string customerSegments);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Selected Customer Segments: \"{selectedSegments}\"")]
    public static partial void SelectedSegments(
        this ILogger<CustomerSegmentsAnnouncementTarget> logger,
        string? selectedSegments);
}
