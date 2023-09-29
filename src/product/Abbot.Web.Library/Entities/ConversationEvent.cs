using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Serious.Abbot.AI;
using Serious.Abbot.Entities.Filters;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Serialization;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Filters;

namespace Serious.Abbot.Entities;

public abstract class ConversationEvent : IEntity, IFilterableEntity<ConversationEvent>
{
    /// <summary>
    /// The ID of this <see cref="ConversationEvent"/>.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The ID of the <see cref="Entities.Conversation"/> this event took place within.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// The <see cref="Conversation"/> this event took place within.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// The platform-specific ID of the thread this event occurred in. A conversation can span multiple threads.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// The ID of the <see cref="Entities.Member"/> who triggered the event.
    /// </summary>
    public int MemberId { get; set; }

    /// <summary>
    /// The <see cref="Entities.Member"/> who triggered the event.
    /// </summary>
    public Member Member { get; set; } = null!;

    /// <summary>
    /// The UTC timestamp of the event.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// The platform-specific ID of the message related to this event.
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// The platform URL of the message related to this event.
    /// </summary>
    public Uri? MessageUrl { get; set; }

    /// <summary>
    /// Additional metadata we want to store with the event.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }

    protected static IEnumerable<IFilterItemQuery<TConversationEvent>> GetFilterItemQueries<TConversationEvent>()
        where TConversationEvent : ConversationEvent
        => ConversationEventFilters.CreateFilters<TConversationEvent>();

    /// <inheritdoc cref="IFilterableEntity{TEntity}.GetFilterItemQueries"/>
    public static IEnumerable<IFilterItemQuery<ConversationEvent>> GetFilterItemQueries() =>
        GetFilterItemQueries<ConversationEvent>();
}

public abstract class ConversationEvent<T> : ConversationEvent where T : JsonSettings
{
    /// <summary>
    /// Deserializes the <see cref="Metadata"/> into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <returns></returns>
    public T? DeserializeMetadata() => Metadata is null
        ? default
        : JsonSettings.FromJson<T>(Metadata);
}

/// <summary>
/// An unknown conversation event.
/// </summary>
public class UnknownConversationEvent : ConversationEvent
{
}

/// <summary>
/// The event recorded when a message was posted to a conversation.
/// </summary>
public class MessagePostedEvent : ConversationEvent<MessagePostedMetadata>
{
    /// <summary>
    /// If set, indicates the name of the external source that originated the message (i.e. Zendesk).
    /// </summary>
    public string? ExternalSource { get; set; }

    /// <summary>
    /// If set, indicates the ID of the original message in the external source (i.e. Zendesk ticket ID).
    /// </summary>
    public string? ExternalMessageId { get; set; }

    /// <summary>
    /// If set, indicates the ID of the author of the original message in the external source (i.e. Zendesk user ID).
    /// </summary>
    public string? ExternalAuthorId { get; set; }

    /// <summary>
    /// If set, indicates a display name fo the author of the original message in the external source.
    /// </summary>
    public string? ExternalAuthor { get; set; }
}

public enum NotificationType
{
    Warning,
    Deadline,
    TicketError,
    TicketPending,
    TicketCreated,
}

public enum NotificationRecipientType
{
    All,

    Actor,

    Assignee,

    [Display(Name = "First Responder")]
    FirstResponder,

    [Display(Name = "Escalation Responder")]
    EscalationResponder,
}

/// <summary>
/// Metadata for the message posted event.
/// </summary>
public record MessagePostedMetadata : JsonSettings
{
    /// <summary>
    /// A list of categories detected in this message by AI
    /// </summary>
    public required IReadOnlyList<Category>? Categories { get; init; }

    /// <summary>
    /// The message text.
    /// </summary>
    public required SecretString? Text { get; init; }

#if DEBUG
    // Helps us debug shit.
    public string? PlainTextString => Text?.Reveal();
#endif

    /// <summary>
    /// Sensitive values found in the message.
    /// </summary>
    public required IReadOnlyList<SensitiveValue> SensitiveValues { get; init; } = Array.Empty<SensitiveValue>();

    /// <summary>
    /// The result of the summarization process.
    /// </summary>
    public SummarizationResult? SummarizationResult { get; init; }

    /// <summary>
    /// The result of trying to match this message with a conversation.
    /// </summary>
    public ConversationMatchAIResult? ConversationMatchAIResult { get; init; }
}

/// <summary>
/// The event recorded when notifications are sent.
/// </summary>
public class NotificationEvent : ConversationEvent<NotificationEvent.Metadata>
{
    public NotificationEvent()
    {
    }

    public NotificationEvent(ConversationNotification notification, bool suppressed = false)
    {
        base.Metadata = new Metadata(notification, suppressed).ToJson();
    }

    /// <summary>
    /// Metadata about a notification event.
    /// </summary>
    /// <param name="Notification">The notification.</param>
    /// <param name="Suppressed">Whether the notification was suppressed because of Notification Settings.</param>
#pragma warning disable CA1724
    public new record Metadata(
#pragma warning restore CA1724
        ConversationNotification Notification,
        bool Suppressed = false
    ) : JsonSettings;
}

/// <summary>
/// The state of the conversation changed by a user, this may have been an explicit change or an implicit one.
/// </summary>
[DebuggerDisplay("New State: {NewState}, Conversation Id: {ConversationId}")]
public class StateChangedEvent : ConversationEvent, IFilterableEntity<StateChangedEvent>
{
    /// <summary>
    /// Indicates the original state at the time the event occurred.
    /// </summary>
    [Column(TypeName = "text")]
    public ConversationState OldState { get; set; }

    /// <summary>
    /// Indicates the state that the conversation changed to.
    /// </summary>
    [Column(TypeName = "text")]
    public ConversationState NewState { get; set; }

    /// <summary>
    /// Indicates if this state change was implicit (the result of some indirect action like posting a message) or
    /// explicit (the result of a direct action like closing/opening a conversation).
    /// </summary>
    public bool Implicit { get; set; }

    public new static IEnumerable<IFilterItemQuery<StateChangedEvent>> GetFilterItemQueries() =>
        GetFilterItemQueries<StateChangedEvent>();
}

/// <summary>
/// The conversation was imported from Slack.
/// </summary>
public class SlackImportEvent : ConversationEvent
{
    // No extra metadata is needed
}

/// <summary>
/// A <see cref="ConversationLink"/> representing an external link was attached to the conversation.
/// </summary>
public class ExternalLinkEvent : ConversationEvent
{
    /// <summary>
    /// The ID of the <see cref="ConversationLink"/> that was linked to the conversation.
    /// </summary>
    public int LinkId { get; set; }

    /// <summary>
    /// The <see cref="ConversationLink"/> that was linked to the conversation.
    /// </summary>
    public ConversationLink Link { get; set; } = null!;
}

public class AttachedToHubEvent : ConversationEvent
{
    /// <summary>
    /// The ID of the <see cref="Hub"/> that the conversation was attached to.
    /// </summary>
    public int HubId { get; set; }

    /// <summary>
    /// The <see cref="Hub"/> that the conversation was attached to.
    /// </summary>
    public Hub Hub { get; set; } = null!;
}

/// <summary>
/// The conversation was summarized.
/// </summary>
[Obsolete("Summarization Result is now part of MessagePostedEvent.")]
public class ConversationSummarizedEvent : ConversationEvent<SummarizationResult>
{
    // No extra metadata is needed
}

public class ConversationClassifiedEvent : ConversationEvent<ClassificationResult>
{
    // No extra metadata is needed
}

/// <summary>
/// An incoming message was matched to a conversation using AI.
/// </summary>
[Obsolete("Conversation Match Result is now part of MessagePostedEvent.")]
public class ConversationMatchedEvent : ConversationEvent<ConversationMatchAIResult>
{
    // No extra metadata is needed
}
