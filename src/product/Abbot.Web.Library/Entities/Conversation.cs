using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities.Filters;
using Serious.Abbot.Messages;
using Serious.Abbot.Repositories;
using Serious.Abbot.Serialization;
using Serious.EntityFrameworkCore;
using Serious.Filters;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a conversation in a <see cref="Room"/> with managed conversations enabled.
/// </summary>
[DebuggerDisplay("{State} - {Title}")]
public class Conversation : OrganizationEntityBase<Conversation>, IFilterableEntity<Conversation>
{
    ConversationProperties? _cachedProperties;
    string? _serializedProperties;

    public Conversation()
    {
        Events = new EntityList<ConversationEvent>();
        Links = new EntityList<ConversationLink>();
        Assignees = new EntityList<Member>();
        Tags = new EntityList<ConversationTag>();
    }

    // Special constructor called by EF Core.
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once UnusedMember.Local
    Conversation(DbContext db)
    {
        Events = new EntityList<ConversationEvent>(db, this, nameof(Events));
        Links = new EntityList<ConversationLink>(db, this, nameof(Links));
        Assignees = new EntityList<Member>(db, this, nameof(Assignees));
        Tags = new EntityList<ConversationTag>(db, this, nameof(Tags));
    }

    /// <summary>
    /// The ID of the room in which the conversation is taking place.
    /// </summary>
    public int RoomId { get; set; }

    /// <summary>
    /// The room in which the conversation is taking place.
    /// </summary>
    public Room Room { get; set; } = null!;

    /// <summary>
    /// The platform-specific ID of the first message in the conversation.
    /// </summary>
    public string FirstMessageId { get; set; } = null!;

    /// <summary>
    /// An array of additional platform-specific thread (top level message) IDs of messages in the conversation.
    /// </summary>
    public List<string> ThreadIds { get; init; } = new();

    /// <summary>
    /// The title of the conversation
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// A summary of the conversation.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets a boolean indicating if the conversation was imported.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ImportedOn))]
    public bool IsImported => ImportedOn is not null;

    /// <summary>
    /// The time that the conversation was imported.
    /// If <c>null</c>, the conversation was not imported.
    /// </summary>
    public DateTime? ImportedOn { get; set; }

    /// <summary>
    /// The time the last message was posted to this conversation.
    /// </summary>
    public DateTime LastMessagePostedOn { get; set; }

    /// <summary>
    /// The time that the conversation first moved to <see cref="ConversationState.Waiting"/>.
    /// </summary>
    public DateTime? FirstResponseOn { get; set; }

    /// <summary>
    /// The most recent time the conversation was closed.
    /// </summary>
    public DateTime? ClosedOn { get; set; }

    /// <summary>
    /// The date that the conversation was transition to a Warning state and a notification was enqueued because the
    /// Wait Time warning SLO was breached. This is reset when the conversation receives a reply.
    /// </summary>
    public DateTime? TimeToRespondWarningNotificationSent { get; set; }

    /// <summary>
    /// The most recent time the conversation was archived.
    /// </summary>
    public DateTime? ArchivedOn { get; set; }

    /// <summary>
    /// The time at which the most recent change to <see cref="State"/> occurred.
    /// </summary>
    public DateTime LastStateChangeOn { get; set; }

    /// <summary>
    /// The status of the conversation
    /// </summary>
    [Column(TypeName = "text")]
    public ConversationState State { get; set; }

    /// <summary>
    /// The ID of the <see cref="Member"/> who started this conversation.
    /// </summary>
    public int StartedById { get; set; }

    /// <summary>
    /// The <see cref="Member"/> who started this conversation.
    /// </summary>
    public Member StartedBy { get; set; } = null!;

    /// <summary>
    /// A list of <see cref="ConversationMember"/> objects representing all the participants in this conversation.
    /// </summary>
    public IList<ConversationMember> Members { get; set; } = new List<ConversationMember>();

    /// <summary>
    /// A list of <see cref="ConversationEvent"/> objects representing the timeline of events for this conversation.
    /// This is NOT loaded by default. It is only loaded when <see cref="IConversationRepository.GetTimelineAsync"/> is called.
    /// </summary>
    public EntityList<ConversationEvent> Events { get; set; }

    /// <summary>
    /// A list of <see cref="ConversationLink"/> objects representing links to external resources within the conversation.
    /// </summary>
    public EntityList<ConversationLink> Links { get; set; }

    /// <summary>
    /// A list of <see cref="Member"/> instances who are assigned to this <see cref="Conversation"/>.
    /// </summary>
    public EntityList<Member> Assignees { get; set; }

    /// <summary>
    /// Maps the set of <see cref="Tag"/> instances applied to this <see cref="Conversation"/>.
    /// </summary>
    public EntityList<ConversationTag> Tags { get; set; }

    /// <summary>
    /// Gets or sets the ID of the <see cref="Hub"/> to which this conversation is linked.
    /// </summary>
    public int? HubId { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Hub"/> to which this conversation is linked.
    /// </summary>
    public Hub? Hub { get; set; }

    /// <summary>
    /// Gets or sets the ID of the thread for this conversation in the <see cref="Hub"/> to which it is linked.
    /// </summary>
    public string? HubThreadId { get; set; }

    /// <summary>
    /// Gets or sets an optional <see cref="ConversationProperties"/> containing additional properties for this conversation.
    /// </summary>
    [Column("Properties", TypeName = "jsonb")]
    public string? SerializedProperties
    {
        get => _serializedProperties;
        set {
            _serializedProperties = value;
            _cachedProperties = null;
        }
    }

    [NotMapped]
    public ConversationProperties Properties
    {
        get {
            if (_cachedProperties is null)
            {
                // _cachedProperties is not initialized, so we need to initialize it.
                // It _might_ be being initialized by another thread, but we'll cover that case later.

                var newValue = JsonSettings.FromJson<ConversationProperties>(SerializedProperties) ?? new();

                // Only set _cachedProperties if it's currently uninitialized.
                Interlocked.CompareExchange(ref _cachedProperties, newValue, null);
            }

            return _cachedProperties.Require();
        }
        set {
            SerializedProperties = value.ToJson();
            _cachedProperties = value;
        }
    }

    /// <inheritdoc cref="IFilterableEntity{TEntity}.GetFilterItemQueries"/>
    public static IEnumerable<IFilterItemQuery<Conversation>> GetFilterItemQueries()
        => ConversationFilters.CreateFilters();
}

/// <summary>
/// A JSON-serialized property bag of additional properties for a conversation.
/// This is used as a flexible way to store some additional properties for a conversation without introducing a new column.
/// These properties cannot easily be queried (though there is limited support for JSON querying in Postgres).
/// </summary>
public record ConversationProperties : JsonSettings
{
    /// <summary>
    /// Gets or sets the AI-generated summary of the conversation.
    /// For legacy reasons, this may also be present in <see cref="Conversation.Summary"/>.
    /// If this value is <c>null</c>, check there.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Gets or sets the AI-generated conclusion of the conversation.
    /// </summary>
    public string? Conclusion { get; init; }

    /// <summary>
    /// If the conclusion for this conversation has been promoted to a <see cref="TaskItem"/>, this is the Id of the task.
    /// </summary>
    public Id<TaskItem>? RelatedTaskItemId { get; init; }

    /// <summary>
    /// Gets or sets the AI-suggested next state for the conversation.
    /// </summary>
    public string? SuggestedState { get; init; }

    /// <summary>
    /// Gets the last message in the Conversation from a supportee.
    /// </summary>
    public string? LastSupporteeMessageId { get; init; }
}

/// <summary>
/// The state of a conversation
/// </summary>
public enum ConversationState
{
    // The names of this enum are used in the database, so they must be unique and stable.

    /// <summary>
    /// The conversation state is unknown.
    /// </summary>
    /// <remarks>
    /// EF Core will load an empty string as the default value for the enum, which is this value.
    /// So this serves as a sentinel for vestigial Conversations that have not been migrated to the new state.
    /// </remarks>
    Unknown = 0,

    /// <summary>
    /// The conversation is newly opened, and nobody from the support team has responded yet.
    /// </summary>
    New,

    /// <summary>
    /// The conversation needs a response from a member of the support team.
    /// </summary>
    [Display(Name = "Needs Response")]
    NeedsResponse,

    /// <summary>
    /// The conversation needs a response from a member of the support team and is past the
    /// deadline.
    /// </summary>
    Overdue,

    /// <summary>
    /// The support team has responded to this conversation and are waiting for a response from the customer.
    /// </summary>
    Waiting,

    /// <summary>
    /// The conversation has been closed, but can still be reopened by a message.
    /// </summary>
    Closed,

    /// <summary>
    /// The conversation has been archived.
    /// The timeline will continue to be updated, but messages can no longer change the state of the conversation.
    /// </summary>
    Archived,

    /// <summary>
    /// The conversation has been snoozed. It will be moved back to a <see cref="NeedsResponse"/> state when the
    /// snooze period is over.
    /// </summary>
    Snoozed,

    /// <summary>
    /// A hidden conversation is a conversation created by the system that is not visible to the customer. For example,
    /// when creating a ticket in a room where conversation management is not enabled.
    /// </summary>
    Hidden,
}

public static class ConversationExtensions
{
    /// <summary>
    /// Gets a platform-specific link to the first message in the conversation.
    /// </summary>
    /// <param name="convo">The <see cref="Conversation"/> to get the link for.</param>
    /// <returns>A URL that will open the first message in the conversation.</returns>
    public static Uri GetFirstMessageUrl(this Conversation convo) =>
        SlackFormatter.MessageUrl(convo.Organization.Domain,
            convo.Room.PlatformRoomId,
            convo.FirstMessageId);

    /// <summary>
    /// Gets a platform-specific link to the specific message in the conversation.
    /// </summary>
    /// <param name="convo">The <see cref="Conversation"/> to get the link for.</param>
    /// <param name="messageId">The message ID within the original thread.</param>
    /// <returns>A URL that will open the specified message in the conversation.</returns>
    public static Uri GetMessageUrl(this Conversation convo, string? messageId) =>
        SlackFormatter.MessageUrl(convo.Organization.Domain,
            convo.Room.PlatformRoomId,
            messageId is { Length: > 0 } ? messageId : convo.FirstMessageId,
            messageId is { Length: > 0 } ? convo.FirstMessageId : null);

    /// <summary>
    /// Converts a <see cref="Conversation"/> into a <see cref="ChatConversation"/>, for sending in API requests.
    /// </summary>
    /// <param name="convo">The <see cref="Conversation"/> to convert.</param>
    /// <param name="webUrl">The URL to the conversation details page.</param>
    /// <returns>A <see cref="ChatConversation"/> with the relevant data.</returns>
    public static ChatConversation ToChatConversation(this Conversation convo, Uri webUrl)
    {
        return new ChatConversation(
            convo.Id.ToString(CultureInfo.InvariantCulture),
            convo.FirstMessageId,
            convo.Title,
            webUrl,
            convo.Room.ToPlatformRoom(),
            convo.StartedBy.ToPlatformUser(),
            new DateTimeOffset(convo.Created, TimeSpan.Zero),
            new DateTimeOffset(convo.LastMessagePostedOn, TimeSpan.Zero),
            convo.Members.Select(m => m.Member.ToPlatformUser()).ToList());
    }

    public static ChatConversationInfo ToChatConversationInfo(this Conversation convo)
    {
        return new ChatConversationInfo(
            $"{convo.Id}",
            convo.FirstMessageId,
            convo.Title);
    }

    /// <summary>
    /// Gets a string representation of a <see cref="ConversationState"/> suitable for displaying to the user.
    /// </summary>
    /// <param name="state">The <see cref="ConversationState"/> to get a display string for.</param>
    /// <returns>The display string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The provided <see cref="ConversationState"/> was not a valid value.</exception>
    public static string ToDisplayString(this ConversationState state) => state switch
    {
        ConversationState.NeedsResponse => "Needs Response",
        ConversationState.Waiting => "Responded",
        ConversationState.Unknown
            or ConversationState.New
            or ConversationState.Snoozed
            or ConversationState.Overdue
            or ConversationState.Closed
            or ConversationState.Archived => state.ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, $"Unknown state {state}"),
    };

    /// <summary>
    /// Returns true if this conversation is waiting on a response. For example,
    /// if it's new, needs response, or overdue.
    /// </summary>
    /// <param name="state">The <see cref="ConversationState"/>.</param>
    public static bool IsWaitingForResponse(this ConversationState state) =>
        WaitingForResponseStates.Contains(state);

    static IEnumerable<ConversationState> AllStates => Enum.GetValues<ConversationState>()
        .Where(s => s != ConversationState.Unknown)
        .ToList();

    public static readonly IReadOnlyList<ConversationState> NotOpenStates = new List<ConversationState>
    {
        ConversationState.Archived,
        ConversationState.Closed
    };

    public static readonly IReadOnlyList<ConversationState> OpenStates = AllStates
        .Where(s => !NotOpenStates.Contains(s))
        .ToList();

    /// <summary>
    /// Returns true if this conversation is open. For example,
    /// if it's new, needs response, waiting, or overdue.
    /// </summary>
    /// <param name="state">The <see cref="ConversationState"/>.</param>
    public static bool IsOpen(this ConversationState state) =>
        !NotOpenStates.Contains(state) && state != ConversationState.Unknown;

    /// <summary>
    /// The states that represent when a conversation is waiting for a response.
    /// </summary>
    public static readonly IReadOnlyList<ConversationState> WaitingForResponseStates = new List<ConversationState>
    {
        ConversationState.New,
        ConversationState.NeedsResponse,
        ConversationState.Overdue,
        ConversationState.Snoozed,
    };

    /// <summary>
    /// Provides an expression usable in queries for conversations that are waiting for a response.
    /// </summary>
    public static Expression<Func<Conversation, bool>> IsOpenExpression() => c =>
        OpenStates.Contains(c.State);

    /// <summary>
    /// Provides an expression usable in queries for conversations that are waiting for a response.
    /// </summary>
    public static Expression<Func<Conversation, bool>> IsWaitingForResponseExpression() => c =>
        WaitingForResponseStates.Contains(c.State);
}
