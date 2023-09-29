using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Represents a set of criteria that can be used to retrieve <see cref="Conversation"/> objects.
/// </summary>
/// <param name="OrganizationId">The ID of the <see cref="Organization"/> to retrieve conversations for.</param>
public record ConversationQuery(int OrganizationId)
{
    public ConversationQuery(Id<Organization> organizationId) : this(organizationId.Value)
    {
    }

    public RoomSelector RoomSelector { get; init; } = RoomSelector.AllRooms;

    public SuggestedTaskSelector SuggestedTaskSelector { get; init; } = SuggestedTaskSelector.All;

    public ConversationStateFilter State { get; init; } = ConversationStateFilter.All;

    /// <summary>
    /// Used to specify the tag to filter on.
    /// </summary>
    public string? Tag { get; init; }

    public ConversationOrdering Ordering { get; init; } = ConversationOrdering.NewestByLastStateChanged;

    /// <summary>
    /// Retrieves all the conversations for rooms where the member is assigned and where the conversation needs a
    /// response.
    /// </summary>
    /// <param name="member">The member who's queue to retrieve.</param>
    /// <returns>The queue of conversations for the specified member.</returns>
    public static ConversationQuery QueueFor(Member member) =>
        new ConversationQuery(member.OrganizationId)
            .InRoomsWhereAssigned(member.Id, RoomRole.FirstResponder)
            .WithState(ConversationStateFilter.NeedsResponse)
            .WithOrdering(ConversationOrdering.Queue);

    /// <summary>
    /// Apply this query to every room.
    /// </summary>
    /// <returns>A new <see cref="ConversationQuery"/> with the all rooms selector.</returns>
    public ConversationQuery InAnyRoom() => this with
    {
        RoomSelector = RoomSelector.AllRooms
    };

    /// <summary>
    /// Returns a new <see cref="ConversationQuery"/> where the <see cref="RoomSelector"/> is set to filter to the specified rooms.
    /// </summary>
    /// <param name="rooms">The IDs of the rooms to filter to.</param>
    /// <returns>An updated <see cref="ConversationQuery"/>.</returns>
    public ConversationQuery InRooms(params int[] rooms) =>
        this with
        {
            RoomSelector = new SpecificRoomsSelector(rooms),
        };

    /// <summary>
    /// Returns a new <see cref="ConversationQuery"/> where the <see cref="RoomSelector"/> is set to filter to rooms where a user is assigned.
    /// </summary>
    /// <param name="memberId">The ID of the <see cref="Member"/> to use when looking up room assignments.</param>
    /// <param name="roles">The set of roles to check for. If empty, selects rooms where the member is assigned any role.</param>
    /// <returns>An updated <see cref="ConversationQuery"/>.</returns>
    public ConversationQuery InRoomsWhereAssigned(int memberId, params RoomRole[] roles) =>
        this with
        {
            RoomSelector = new AssignedRoomsSelector(memberId, roles),
        };

    /// <summary>
    /// Returns a new <see cref="ConversationQuery"/> where the <see cref="RoomSelector"/> is set to filter to rooms
    /// where a user is responder either directly, or via being a default responder.
    /// </summary>
    /// <param name="member">The <see cref="Member"/> to use when looking up room assignments.</param>
    /// <returns>An updated <see cref="ConversationQuery"/>.</returns>
    public ConversationQuery InRoomsWhereResponder(Member member) =>
        this with
        {
            RoomSelector = new ResponderRoomSelector(member),
        };

    public ConversationQuery WithSuggestedTask() =>
        this with
        {
            SuggestedTaskSelector = new SuggestedTaskSelector(),
        };

    /// <summary>
    /// Returns a new <see cref="ConversationQuery"/> where the <see cref="State"/> is set to filter to the specified state value.
    /// </summary>
    /// <param name="state">The <see cref="ConversationStateFilter"/> to use when filtering conversations.</param>
    /// <returns>An updated <see cref="ConversationQuery"/>.</returns>
    public ConversationQuery WithState(ConversationStateFilter state) =>
        this with
        {
            State = state,
        };


    public ConversationQuery WithTag(string? tag) =>
        this with
        {
            Tag = tag,
        };

    /// <summary>
    /// Returns a new <see cref="ConversationQuery"/> where the <see cref="Ordering"/> is set to the specified <see cref="ConversationOrdering"/> value.
    /// </summary>
    /// <param name="ordering">The <see cref="ConversationOrdering"/> to use when ordering conversations.</param>
    /// <returns>An updated <see cref="ConversationQuery"/>.</returns>
    public ConversationQuery WithOrdering(ConversationOrdering ordering) =>
        this with
        {
            Ordering = ordering,
        };
}

/// <summary>
/// Specifies how conversations will be ordered.
/// </summary>
public enum ConversationOrdering
{
    /// <summary>
    /// Conversations will be ordered by their last state change date, with the newest first.
    /// </summary>
    NewestByLastStateChanged,

    /// <summary>
    /// Conversations will be arranged for the queue view.
    /// </summary>
    Queue,
}

/// <summary>
/// Specifies options for filtering by conversation state.
/// This is not a 1:1 mapping to the <see cref="ConversationState" /> enum.
/// Rather it's a 1:1 mapping to the "state" options we show on the UI.
/// </summary>
public enum ConversationStateFilter
{
    /// <summary>
    /// Retrieves all conversations, regardless of state.
    /// </summary>
    All,

    /// <summary>
    /// Retrieves all open conversations.
    /// </summary>
    Open,

    /// <summary>
    /// Retrieves all new conversations.
    /// </summary>
    New,

    /// <summary>
    /// Returns conversations that are in need of a response AND have breached the warning SLO for their room, if any.
    /// </summary>
    Overdue,

    /// <summary>
    /// Returns conversations that are in need of a response, regardless of if they are overdue.
    /// </summary>
    [Display(Name = "Needs Response")]
    NeedsResponse,

    /// <summary>
    /// Returns conversations that have already been responded-to and don't need further response at this time.
    /// </summary>
    Responded,

    /// <summary>
    /// Returns conversations that have been closed.
    /// </summary>
    Closed,

    /// <summary>
    /// Returns conversations that are archived.
    /// </summary>
    Archived,
}

public static class ConversationQueryExtensions
{
    public static ConversationStateFilter? ToStateFilter(this Conversation conversation) =>
        conversation.State switch
        {
            ConversationState.Waiting => ConversationStateFilter.Responded,
            ConversationState.Closed => ConversationStateFilter.Closed,
            var s when s.IsWaitingForResponse() => ConversationStateFilter.NeedsResponse,
            _ => null,
        };

    /// <summary>
    /// Applies this selector to the given query.
    /// </summary>
    /// <param name="filter">The <see cref="ConversationStateFilter"/> to apply to the query.</param>
    /// <param name="query">The query to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public static IQueryable<Conversation> Apply(this ConversationStateFilter filter, IQueryable<Conversation> query)
    {
        return filter switch
        {
            ConversationStateFilter.All => query,
            ConversationStateFilter.Open => query.Where(ConversationExtensions.IsOpenExpression()),
            ConversationStateFilter.New => query.Where(c => c.State == ConversationState.New),
            ConversationStateFilter.Overdue => query
                .Where(c => c.Room.TimeToRespond.Deadline != null // The room has an SLO
                    && c.State == ConversationState.Overdue),
            ConversationStateFilter.NeedsResponse => query.Where(ConversationExtensions.IsWaitingForResponseExpression()),
            ConversationStateFilter.Responded => query.Where(c => c.State == ConversationState.Waiting),
            ConversationStateFilter.Closed => query.Where(c => c.State == ConversationState.Closed),
            ConversationStateFilter.Archived => query.Where(c => c.State == ConversationState.Archived),
            _ => throw new ArgumentOutOfRangeException(nameof(filter),
                filter,
                $"Unknown conversation state filter value '{filter}'"),
        };
    }

    public static IQueryable<Conversation> ApplyTagFilter(
        this ConversationQuery conversationQuery,
        IQueryable<Conversation> query)
    {
        return conversationQuery.Tag switch
        {
            // For some reason, trying to query conversations with no tags always returns 0 results.
            "`untagged`" => query.Where(c => !c.Tags.Any()),
            { Length: > 0 } tagName => query.Where(c => c.Tags.Any(t => t.Tag.Name == tagName)),
            _ => query,
        };
    }

    /// <summary>
    /// Applies the ordering to the given query.
    /// </summary>
    /// <param name="ordering">The <see cref="ConversationOrdering"/> to apply to the query.</param>
    /// <param name="query">The query to apply the ordering to.</param>
    /// <param name="nowUtc">The current time in UTC, for computing SLOs.</param>
    /// <returns>The new query, with the ordering applied.</returns>
    public static IQueryable<Conversation> Apply(this ConversationOrdering ordering, IQueryable<Conversation> query,
        DateTime nowUtc)
    {
        // Apply ordering
        return ordering switch
        {
            ConversationOrdering.NewestByLastStateChanged =>
                query.OrderByDescending(c => c.LastStateChangeOn)
                    .ThenByDescending(c => c.Id),
            ConversationOrdering.Queue => ApplyQueueOrdering(query, nowUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(ordering),
                ordering,
                $"Unknown conversation ordering value '{ordering}'"),
        };
    }

    static IQueryable<Conversation> ApplyQueueOrdering(IQueryable<Conversation> query, DateTime nowUtc)
    {
        // Compute values needed for ordering
        var orderQuery = query
            .Select(c => new {
                Conversation = c,
                InStateFor = nowUtc - c.LastStateChangeOn,
                CriticalSlo = ConversationExtensions.WaitingForResponseStates.Contains(c.State) ? c.Room.TimeToRespond.Deadline : TimeSpan.Zero,
                WarningSlo = ConversationExtensions.WaitingForResponseStates.Contains(c.State) ? c.Room.TimeToRespond.Warning : TimeSpan.Zero,
            })
            .Select(c => new {
                c.Conversation,
                c.InStateFor,
                PastCriticalSlo = c.CriticalSlo == TimeSpan.Zero
                    ? TimeSpan.Zero
                    : c.InStateFor - c.CriticalSlo,
                PastWarningSlo = c.WarningSlo == TimeSpan.Zero
                    ? TimeSpan.Zero
                    : c.InStateFor - c.WarningSlo,
            })
            .Select(c => new {
                c.Conversation,
                c.InStateFor,
                PastCriticalSlo = c.PastCriticalSlo <= TimeSpan.Zero
                    ? TimeSpan.Zero
                    : c.PastCriticalSlo,
                PastWarningSlo = c.PastWarningSlo <= TimeSpan.Zero
                    ? TimeSpan.Zero
                    : c.PastWarningSlo,
            });

        // Finally, we prioritize the conversations based on the following rules:
        // 1. Conversations that have breached their critical SLO
        // 2. Conversations that have breached their warning SLO
        // 3. Other conversations waiting for a response from oldest to newest (by the date they last changed state)
        // It might appear that you could just order by descending last state change date,
        // and you'd naturally get "Critical" SLO breaches then "Warning" SLO breaches then in-SLO conversations.
        // But that only works _in a single room_.
        // If you're viewing across multiple rooms, we need to explicitly order by how far past SLO they are.
        // Once we've got through the conversations that are past-SLO, we can just order in-SLO conversations by state change date.
        orderQuery = orderQuery.OrderByDescending(c => c.PastCriticalSlo)
            .ThenByDescending(c => c.PastWarningSlo)
            .ThenByDescending(c => c.InStateFor);

        return orderQuery.Select(o => o.Conversation);
    }
}

/// <summary>
/// A base class for room "selectors" which are used to reference a particular set of rooms given specific criteria.
/// <seealso cref="SpecificRoomsSelector"/>
/// <seealso cref="AssignedRoomsSelector"/>
/// </summary>
public abstract class RoomSelector : IEquatable<RoomSelector>, ISelector<Conversation>, ISelector<Room>, ISelector<MetricObservation>
{
    public static readonly RoomSelector AllRooms = new AllRoomsSelector();

    /// <summary>
    /// Applies this selector to the given <see cref="Conversation"/> query.
    /// </summary>
    /// <param name="queryable">The query to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public abstract IQueryable<Conversation> Apply(IQueryable<Conversation> queryable);

    /// <summary>
    /// Applies this selector to the given <see cref="MetricObservation"/> query.
    /// </summary>
    /// <param name="queryable">The query to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public abstract IQueryable<MetricObservation> Apply(IQueryable<MetricObservation> queryable);

    /// <summary>
    /// Applies this selector to the given <see cref="ConversationEvent"/> query.
    /// </summary>
    /// <param name="query">The query to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public abstract IQueryable<TConversationEvent> Apply<TConversationEvent>(IQueryable<TConversationEvent> query)
        where TConversationEvent : ConversationEvent;

    /// <summary>
    /// Applies this selector to the given <see cref="Room"/> query.
    /// </summary>
    /// <param name="queryable">The query to apply the selector to.</param>
    /// <returns>The new query, with the selector applied.</returns>
    public abstract IQueryable<Room> Apply(IQueryable<Room> queryable);

    class AllRoomsSelector : RoomSelector
    {
        public override IQueryable<Conversation> Apply(IQueryable<Conversation> query) =>
            query;

        public override IQueryable<MetricObservation> Apply(IQueryable<MetricObservation> query) => query;

        public override IQueryable<TConversationEvent> Apply<TConversationEvent>(IQueryable<TConversationEvent> query) =>
            query;

        public override IQueryable<Room> Apply(IQueryable<Room> query) =>
            query;

        public override bool Equals(RoomSelector? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => 0;
        public override string ToString() => "AllRooms";
    }

    public abstract bool Equals(RoomSelector? other);
    public override bool Equals(object? obj) => obj is RoomSelector other && Equals(other);
    public override int GetHashCode() => 0;
}

public class CustomerRoomSelector : RoomSelector
{
    public Id<Customer> CustomerId { get; }

    public CustomerRoomSelector(Id<Customer> customerId)
    {
        CustomerId = customerId;
    }

    public override IQueryable<Conversation> Apply(IQueryable<Conversation> queryable)
        => queryable.Where(c => c.Room.CustomerId == CustomerId);

    public override IQueryable<MetricObservation> Apply(IQueryable<MetricObservation> queryable)
        => queryable.Where(c => c.Room!.CustomerId == CustomerId);

    public override IQueryable<TConversationEvent> Apply<TConversationEvent>(IQueryable<TConversationEvent> query)
        => query.Where(c => c.Conversation.Room.CustomerId == CustomerId);

    public override IQueryable<Room> Apply(IQueryable<Room> queryable)
        => queryable.Where(r => r.CustomerId == CustomerId);

    public override bool Equals(RoomSelector? other)
        => other is CustomerRoomSelector customerRoomSelector && customerRoomSelector.CustomerId == CustomerId;
}

/// <summary>
/// Selects rooms where the provided user is a responder either directly or indirectly by being a default responder.
/// </summary>
public class ResponderRoomSelector : RoomSelector
{
    /// <summary>
    /// The <see cref="Member"/> to use when looking up room assignments.
    /// </summary>
    public Member Member { get; }

    /// <summary>
    /// Creates an <see cref="ResponderRoomSelector"/>.
    /// </summary>
    /// <param name="member">The <see cref="Member"/> to use when looking up room assignments.</param>
    public ResponderRoomSelector(Member member)
    {
        Member = member;
    }

    public override IQueryable<Conversation> Apply(IQueryable<Conversation> queryable)
    {
        return queryable.Where(c => c.Room.Assignments.Any(a => a.MemberId == Member.Id)
                || (Member.IsDefaultFirstResponder && c.Room.Assignments.All(a => a.Role != RoomRole.FirstResponder))
                || (Member.IsDefaultEscalationResponder && c.Room.Assignments.All(a => a.Role != RoomRole.EscalationResponder)));
    }

    public override IQueryable<MetricObservation> Apply(IQueryable<MetricObservation> queryable)
    {
        return queryable.Where(c => c.Room!.Assignments.Any(a => a.MemberId == Member.Id)
                                || (Member.IsDefaultFirstResponder && c.Room.Assignments.All(a => a.Role != RoomRole.FirstResponder))
                                || (Member.IsDefaultEscalationResponder && c.Room.Assignments.All(a => a.Role != RoomRole.EscalationResponder)));
    }

    public override IQueryable<TConversationEvent> Apply<TConversationEvent>(IQueryable<TConversationEvent> query)
    {
        return query.Where(c => c.Conversation.Room.Assignments.Any(a => a.MemberId == Member.Id)
                            || (Member.IsDefaultFirstResponder && c.Conversation.Room.Assignments.All(a => a.Role != RoomRole.FirstResponder))
                            || (Member.IsDefaultEscalationResponder && c.Conversation.Room.Assignments.All(a => a.Role != RoomRole.EscalationResponder)));
    }

    public override IQueryable<Room> Apply(IQueryable<Room> queryable)
    {
        return queryable.Where(r => r.Assignments.Any(a => a.MemberId == Member.Id)
                                || (Member.IsDefaultFirstResponder && r.Assignments.All(a => a.Role != RoomRole.FirstResponder))
                                || (Member.IsDefaultEscalationResponder && r.Assignments.All(a => a.Role != RoomRole.EscalationResponder)));
    }

    public override bool Equals(RoomSelector? other) =>
        other is AssignedRoomsSelector o
        && o.MemberId == Member.Id;

    public override bool Equals(object? obj) => obj is RoomSelector other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Member.Id);

    public override string ToString() => $"ResponderRooms({Member.Id})";
}

/// <summary>
/// Selects rooms where the provided user is assigned any of the provided roles.
/// </summary>
public class AssignedRoomsSelector : RoomSelector
{
    /// <summary>
    /// The ID of the <see cref="Member"/> to use when looking up room assignments.
    /// </summary>
    public int MemberId { get; }

    /// <summary>
    /// The set of roles to check for. If empty, selects rooms where the member is assigned any role.
    /// </summary>
    public IReadOnlyList<RoomRole> Roles { get; }

    /// <summary>
    /// Creates an <see cref="AssignedRoomsSelector"/>.
    /// </summary>
    /// <param name="memberId">The ID of the <see cref="Member"/> to use when looking up room assignments.</param>
    /// <param name="roles">The set of roles to check for. If empty, selects rooms where the member is assigned any role.</param>
    public AssignedRoomsSelector(int memberId, params RoomRole[] roles)
    {
        MemberId = memberId;
        Roles = roles;
    }

    public override IQueryable<Conversation> Apply(IQueryable<Conversation> queryable)
    {
        return Roles.Count > 0
            ? queryable.Where(c => c.Room.Assignments.Any(a => a.MemberId == MemberId && Roles.Contains(a.Role)))
            : queryable.Where(c => c.Room.Assignments.Any(a => a.MemberId == MemberId));
    }

    public override IQueryable<MetricObservation> Apply(IQueryable<MetricObservation> queryable)
    {
        return Roles.Count > 0
            ? queryable.Where(c => c.Room!.Assignments.Any(a => a.MemberId == MemberId && Roles.Contains(a.Role)))
            : queryable.Where(c => c.Room!.Assignments.Any(a => a.MemberId == MemberId));
    }

    public override IQueryable<TConversationEvent> Apply<TConversationEvent>(IQueryable<TConversationEvent> query)
    {
        return Roles.Count > 0
            ? query.Where(c => c.Conversation.Room.Assignments.Any(a => a.MemberId == MemberId && Roles.Contains(a.Role)))
            : query.Where(c => c.Conversation.Room.Assignments.Any(a => a.MemberId == MemberId));
    }

    public override IQueryable<Room> Apply(IQueryable<Room> queryable)
    {
        return Roles.Count > 0
            ? queryable.Where(r => r.Assignments.Any(a => a.MemberId == MemberId && Roles.Contains(a.Role)))
            : queryable.Where(r => r.Assignments.Any(a => a.MemberId == MemberId));
    }

    public override bool Equals(RoomSelector? other) =>
        other is AssignedRoomsSelector o
        && o.MemberId == MemberId
        && o.Roles.SequenceEqual(Roles);

    public override bool Equals(object? obj) => obj is RoomSelector other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(MemberId, Roles);

    public override string ToString() => $"AssignedRooms({MemberId}, {string.Join(", ", Roles)})";
}

/// <summary>
/// Selects rooms by their room ID.
/// </summary>
public class SpecificRoomsSelector : RoomSelector
{
    public IReadOnlyList<int> RoomIds { get; }

    /// <summary>
    /// Creates a <see cref="SpecificRoomsSelector"/>.
    /// </summary>
    /// <param name="roomIds">The IDs of the rooms to use when looking up room assignments. At least one must be provided.</param>
    public SpecificRoomsSelector(params int[] roomIds)
    {
        if (roomIds.Length == 0)
        {
            throw new ArgumentException("At least one room ID must be provided.", nameof(roomIds));
        }

        RoomIds = roomIds;
    }

    public override IQueryable<Conversation> Apply(IQueryable<Conversation> queryable)
    {
        return RoomIds.Count > 0
            ? queryable.Where(c => RoomIds.Contains(c.RoomId))
            : queryable;
    }

    public override IQueryable<MetricObservation> Apply(IQueryable<MetricObservation> queryable)
    {
        return RoomIds.Count > 0
            ? queryable.Where(c => RoomIds.Contains(c.RoomId))
            : queryable;
    }

    public override IQueryable<TConversationEvent> Apply<TConversationEvent>(IQueryable<TConversationEvent> query)
    {
        return RoomIds.Count > 0
            ? query.Where(c => RoomIds.Contains(c.Conversation.RoomId))
            : query;
    }

    public override IQueryable<Room> Apply(IQueryable<Room> queryable)
    {
        return RoomIds.Count > 0
            ? queryable.Where(r => RoomIds.Contains(r.Id))
            : queryable;
    }

    public override bool Equals(RoomSelector? other) =>
        other is SpecificRoomsSelector o
        && o.RoomIds.SequenceEqual(RoomIds);
    public override bool Equals(object? obj) => obj is RoomSelector other && Equals(other);
    public override int GetHashCode() => RoomIds.GetHashCode();
    public override string ToString() => $"Rooms({string.Join(", ", RoomIds)})";
}
