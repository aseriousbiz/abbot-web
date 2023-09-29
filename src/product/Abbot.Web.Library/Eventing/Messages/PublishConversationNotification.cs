using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

public class PublishConversationNotification : IProvidesLoggerScope, IOrganizationMessage
{
    public required Id<Organization> OrganizationId { get; init; }
    public required Id<Conversation> ConversationId { get; init; }
    public required ConversationNotification Notification { get; init; }
    public required bool Broadcast { get; init; }

    static readonly Func<ILogger, int, IDisposable?> LoggerScope =
        LoggerMessage.DefineScope<int>(
            "PublishHubThreadNotification ConversationId={ConversationId}");

    public IDisposable? BeginScope(ILogger logger) =>
        LoggerScope(logger, ConversationId);
}

public class ConversationNotification
{
    /// <summary>
    /// Gets or set the <see cref="NotificationType"/> for this notification.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Gets or sets the icon for this notification.
    /// This should be a Unicode Emoji, but if absolutely necessary it can be a Slack emoji reference (":smile:")
    /// </summary>
    public required string Icon { get; set; }

    /// <summary>
    /// Gets or sets the headline for this notification.
    /// </summary>
    public required string Headline { get; set; }

    /// <summary>
    /// Gets or sets the message for this notification.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets a list of mention groups, which will be rendered as a list of mentions in the notification.
    /// </summary>
    public IList<MentionGroup> MentionGroups { get; set; } = new List<MentionGroup>();
}

/// <summary>
/// Represents a group of mentioned users in a notification.
/// This will be rendered as: '<see cref="RecipientType"/>: @user1 @user2 @user3'
/// </summary>
/// <param name="RecipientType">The <see cref="NotificationRecipientType"/> of the group being mentioned.</param>
/// <param name="MentionIds">A list of chat platform user IDs to mention in this group.</param>
public record MentionGroup(NotificationRecipientType RecipientType, IReadOnlyList<string> MentionIds);
