using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Serious.Abbot.Entities;

public class AnnouncementEvent : LegacyAuditEvent
{
    [NotMapped]
    public override bool HasDetails => SerializedProperties is not null;
}

/// <summary>
/// Additional information about a scheduled announcement.
/// </summary>
/// <param name="SourceMessageId">The Id of the source message that is scheduled to be broadcast.</param>
/// <param name="Text">The announcement text. We only store this when the announcement is sent so we have a snapshot of what was sent.</param>
/// <param name="ScheduledDateUtc">The date that the announcement is supposed to be sent.</param>
/// <param name="DateStartedUtc">The date the announcement started sending.</param>
/// <param name="DateCompletedUtc">The date the announcement completed.</param>
/// <param name="Messages">The set of rooms (with information about the sent message if applicable), the announcement should be posted to.</param>
public record AnnouncementInfo(
    string SourceMessageId,
    DateTime? ScheduledDateUtc,
    AnnouncementRoomInfo SourceRoom,
    IReadOnlyList<AnnouncementRoomInfo> Messages,
    string? Text = null,
    DateTime? DateStartedUtc = null,
    DateTime? DateCompletedUtc = null)
{
    public static AnnouncementInfo FromAnnouncement(Announcement announcement)
    {
        return new AnnouncementInfo(announcement.SourceMessageId,
            announcement.ScheduledDateUtc,
            AnnouncementRoomInfo.FromRoom(announcement.SourceRoom),
            announcement.Messages.Select(AnnouncementMessageInfo.FromAnnouncementMessage).ToList(),
            announcement.Text,
            announcement.DateStartedUtc,
            announcement.DateCompletedUtc);
    }
}

/// <summary>
/// Information about a room where an announcement is scheduled to be sent to.
/// </summary>
/// <param name="PlatformRoomId">The channel Id.</param>
/// <param name="Name">The name of the room.</param>
public record AnnouncementRoomInfo(string PlatformRoomId, string Name)
{
    public static AnnouncementRoomInfo FromRoom(Room room) => new(room.PlatformRoomId, room.Name ?? "(unknown)");
}

/// <summary>
/// Information about a room where an announcement is scheduled to be sent to.
/// </summary>
/// <param name="PlatformRoomId">The channel Id.</param>
/// <param name="Name">The name of the room.</param>
/// <param name="MessageId">If the message has been sent, the message Id.</param>
/// <param name="SentDate">The date the message was sent to the room.</param>
/// <param name="ErrorMessage">The error message, if any.</param>
public record AnnouncementMessageInfo(
    string PlatformRoomId,
    string Name,
    string? MessageId = null,
    DateTime? SentDate = null,
    string? ErrorMessage = null) : AnnouncementRoomInfo(PlatformRoomId, Name)
{
    public AnnouncementMessageInfo(
        AnnouncementRoomInfo room,
        string? messageId,
        DateTime? sentDate,
        string? errorMessage)
        : this(room.PlatformRoomId, room.Name, messageId, sentDate, errorMessage)
    {
    }

    public static AnnouncementMessageInfo FromAnnouncementMessage(AnnouncementMessage message)
    {
        return new AnnouncementMessageInfo(
            FromRoom(message.Room),
            message.MessageId,
            message.SentDateUtc,
            message.ErrorMessage);
    }
}
