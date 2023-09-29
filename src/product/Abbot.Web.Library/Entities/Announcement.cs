using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// An announcement is a message that will be posted to multiple channels.
/// </summary>
public class Announcement : TrackedEntityBase<Announcement>, IOrganizationEntity, IRecoverableEntity, IAuditableEntity
{
    /// <summary>
    /// The announcement text. We only store this when the announcement is sent so we have a snapshot of what was
    /// sent.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// The set of rooms to post the announcement to.
    /// </summary>
    public IList<AnnouncementMessage> Messages { get; set; } = new List<AnnouncementMessage>();

    /// <summary>
    /// The set of customer segments to post the announcement to.
    /// </summary>
    /// <remarks>
    /// If this contains any segments, then the <see cref="Messages"/> collection is only populated at the time
    /// we're about to send the announcement.
    /// </remarks>
    public IList<AnnouncementCustomerSegment> CustomerSegments { get; set; } =
        new List<AnnouncementCustomerSegment>();

    /// <summary>
    /// The date to schedule the announcement. Set to <c>null</c> if it's meant to be sent immediately.
    /// </summary>
    public DateTime? ScheduledDateUtc { get; set; }

    /// <summary>
    /// The date that sending this announcement was started. This is used to determine if the announcement has been
    /// sent or not. When this is not null, rescheduling a job is no longer allowed.
    /// </summary>
    public DateTime? DateStartedUtc { get; set; }

    /// <summary>
    /// The date that sending this announcement was completed. This is used to determine if the announcement has been
    /// sent or not. When this is not null, rescheduling a job is no longer allowed.
    /// </summary>
    public DateTime? DateCompletedUtc { get; set; }

    /// <summary>
    /// The source message used to create this announcement.
    /// </summary>
    public string SourceMessageId { get; set; } = null!;

    /// <summary>
    /// The room where the source message was created.
    /// </summary>
    public Room SourceRoom { get; set; } = null!;

    /// <summary>
    /// The Id of the room where the source message was created.
    /// </summary>
    public int SourceRoomId { get; set; }

    /// <summary>
    /// The Hangfire job Id of the job that is currently sending the announcement.
    /// </summary>
    public string? ScheduledJobId { get; set; }

    /// <summary>
    /// The Id of the organization this announcement belongs to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The organization this announcement belongs to.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// Whether or not this announcement is deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Whether or not this announcement should send as the Organization bot.
    /// </summary>
    public bool SendAsBot { get; set; }

    /// <summary>
    /// Create an audit event for an <see cref="Announcement"/>.
    /// </summary>
    /// <param name="auditOperation">The type of change.</param>
    public AuditEventBase? CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var description = auditOperation switch
        {
            AuditOperation.Created =>
                $"Announcement scheduled for {ScheduledDateUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "immediate release"}.",
            AuditOperation.Changed => null,
            AuditOperation.Removed =>
                $"Announcement schedule for {ScheduledDateUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "immediate release"} has been unscheduled and deleted.",
            _ => throw new UnreachableException()
        };

        return description is not null
            ? new AnnouncementEvent
            {
                EntityId = Id,
                Description = description,
                Properties = AnnouncementInfo.FromAnnouncement(this),
            }
            : null;
    }
}

/// <summary>
/// A mapping of <see cref="Announcement"/> to <see cref="CustomerTag"/>.
/// </summary>
public class AnnouncementCustomerSegment : EntityBase<AnnouncementCustomerSegment>
{
    /// <summary>
    /// The Id of the <see cref="Announcement" /> that will be sent to the rooms of the customers in the
    /// <see cref="CustomerTag" /> customer segments.
    /// </summary>
    public int AnnouncementId { get; set; }

    /// <summary>
    /// The <see cref="Announcement" /> that will be sent to the rooms of the customers in the
    /// <see cref="CustomerTag" /> customer segments.
    /// </summary>
    public Announcement Announcement { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="CustomerTag"/> the <see cref="Announcement"/> will be sent to.
    /// </summary>
    public int CustomerTagId { get; set; }

    /// <summary>
    /// The <see cref="CustomerTag"/> the <see cref="Announcement"/> will be sent to.
    /// </summary>
    public CustomerTag CustomerTag { get; set; } = null!;
}

/// <summary>
/// The per-room message for the announcement. If <see cref="SentDateUtc"/> is null, then the message has yet to be
/// sent to the room. If it is not null, then the message has been sent and this tracks information about the sent
/// message such as the message ID.
/// </summary>
public class AnnouncementMessage : EntityBase<AnnouncementMessage>
{
    /// <summary>
    /// The UTC date the announcement was sent to the room. If null, the announcement has not been sent to the room yet.
    /// </summary>
    public DateTime? SentDateUtc { get; set; }

    /// <summary>
    /// The error message, if any, when sending the message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The room the announcement to send to.
    /// </summary>
    public Room Room { get; set; } = null!;

    /// <summary>
    /// The Id of the room.
    /// </summary>
    public int RoomId { get; set; }

    /// <summary>
    /// The announcement this message is for.
    /// </summary>
    public Announcement Announcement { get; set; } = null!;

    /// <summary>
    /// The Id of the announcement this message is for.
    /// </summary>
    public int AnnouncementId { get; set; }

    /// <summary>
    /// The message ID of the announcement according to the platform (aka Slack message Id of the posted message).
    /// </summary>
    public string? MessageId { get; set; }
}
