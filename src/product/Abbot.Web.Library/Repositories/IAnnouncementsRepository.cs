using Serious.Abbot.Entities;
using Serious.Collections;

namespace Serious.Abbot.Repositories;

/// <summary>
/// A repository for announcements.
/// </summary>
public interface IAnnouncementsRepository : IOrganizationScopedRepository<Announcement>
{
    /// <summary>
    /// Returns a paginated list of announcements that have not yet been completely sent.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="pastNumberOfDays">We only show announcements in the past number of days. If null, get all completed.</param>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    Task<IPaginatedList<Announcement>> GetCompletedAnnouncementsAsync(
        int page,
        int pageSize,
        int? pastNumberOfDays,
        Organization organization);

    /// <summary>
    /// Returns a paginated list of announcements scheduled to be sent in the future.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    Task<IPaginatedList<Announcement>> GetUncompletedAnnouncementsAsync(
        int page,
        int pageSize,
        Organization organization);

    /// <summary>
    /// Returns an announcement for the given message.
    /// </summary>
    /// <param name="platformRoomId">The platform-specific id for the room.</param>
    /// <param name="messageId">The platform-specific id for the message.</param>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    Task<Announcement?> GetAnnouncementFromMessageAsync(
        string platformRoomId,
        string messageId,
        Organization organization);

    /// <summary>
    /// Retrieves an announcement by its ID without validating it against an organization. This is called when
    /// scheduling an announcement via a trusted code path.
    /// </summary>
    /// <param name="id">The Id of the announcement.</param>
    /// <returns>The announcement.</returns>
    Task<Announcement> RequireForSchedulingAsync(int id);

    /// <summary>
    /// Retrieves an <see cref="AnnouncementMessage"/> (and includes its parent <see cref="Announcement"/> and
    /// target <see cref="Room"/> entities) by its ID without validating it against an organization.
    /// This is called when about to send an <see cref="Announcement"/> to a room.
    /// </summary>
    /// <param name="id">The Id of the announcement.</param>
    /// <returns>The announcement.</returns>
    Task<AnnouncementMessage> RequireMessageForSendingAsync(int id);

    /// <summary>
    /// Updates the start date for an announcement's broadcast.
    /// </summary>
    /// <param name="announcement">The announcement to update.</param>
    /// <param name="text">The text of the announcement. We wait till the last minute to grab it from the source message.</param>
    /// <param name="utcNow">The current date.</param>
    Task UpdateAnnouncementTextAndStartDateAsync(Announcement announcement, string text, DateTime utcNow);

    /// <summary>
    /// Updates the send date for an <see cref="Announcement"/>'s message to a room.
    /// </summary>
    /// <param name="message">The message that was sent.</param>
    /// <param name="messageId">The platform message Id for the sent message (In Slack, this is the message timestamp). This is <c>null</c> if sending the message was unsuccessful.</param>
    /// <param name="errorMessage">If not successful, this contains an error message.</param>
    /// <param name="utcNow">When it was sent.</param>
    Task UpdateMessageSendCompletedAsync(
        AnnouncementMessage message,
        string? messageId,
        string? errorMessage,
        DateTime utcNow);

    /// <summary>
    /// Retrieves the announcement, sets it completed if all messages have been sent. Does it in a transaction
    /// and returns <c>true</c> if this call set the announcement to completed.
    /// </summary>
    /// <param name="announcementId">The Id of the announcement.</param>
    /// <returns><c>true</c> if this call set the announcement to completed. <c>false</c> if it was already completed or is not complete.</returns>
    Task<bool> SetAnnouncementCompletedAsync(Id<Announcement> announcementId);
}
