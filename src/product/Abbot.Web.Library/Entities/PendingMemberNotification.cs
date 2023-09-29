namespace Serious.Abbot.Entities;

/// <summary>
/// A table used to enqueue notifications to responders when conversations become in a warning or overdue state.
/// </summary>
/// <remarks>
/// When a conversation's state changes hits the warning period or Overdue state, we enqueue a pending notification
/// here. When we send the notification, we delete the record.
/// </remarks>
public class PendingMemberNotification : OrganizationEntityBase<PendingMemberNotification>
{
    /// <summary>
    /// The Id of the member to send a notification to.
    /// </summary>
    public required int MemberId { get; init; }

    /// <summary>
    /// The member to send this to.
    /// </summary>
    public required Member Member { get; init; }

    /// <summary>
    /// The Id of the conversation that is overdue or about to be overdue.
    /// </summary>
    public required int ConversationId { get; init; }

    /// <summary>
    /// The conversation that is overdue or about to be overdue.
    /// </summary>
    public required Conversation Conversation { get; init; }

    /// <summary>
    /// The date and time this notification was sent.
    /// </summary>
    public DateTime? DateSentUtc { get; set; }

    /// <summary>
    /// When enqueued outside working hours, this is the date and time the notification should be sent.
    /// </summary>
    public DateTime? NotBeforeUtc { get; set; }
}
