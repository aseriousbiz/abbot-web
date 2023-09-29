using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Serious.Abbot.Entities;
using Serious.Abbot.Models.Api;
using Serious.Filters;

[assembly: InternalsVisibleTo("Abbot.Web.Library.Tests")]

namespace Serious.Abbot.Repositories;

public class InsightsRepository : IInsightsRepository
{
    readonly AbbotContext _db;

    public InsightsRepository(AbbotContext db)
    {
        _db = db;
    }

    IQueryable<Conversation> GetConversationsCreatedQueryable(int organizationId)
        => _db.Conversations
            .Include(c => c.StartedBy)
            .Where(e => e.OrganizationId == organizationId)
        ;

    // Get all conversation events that are state changes for the organization without a time range.
    IQueryable<StateChangedEvent> GetConversationEventsQueryable(int organizationId)
        => _db.ConversationEvents
            .OfType<StateChangedEvent>()
            // We can filter out all snoozed and unsnoozed state changes because they are not relevant for the insights.
            .Where(e => e.OldState != ConversationState.Snoozed)
            .Where(e => e.NewState != ConversationState.Snoozed)
            .Where(e => e.NewState != ConversationState.Unknown)
            .Where(e => e.NewState != e.OldState) // We have some old bad data we want to filter out.
            .Include(e => e.Conversation)
            .ThenInclude(c => c.Room)
            .ThenInclude(r => r.Assignments)
            .Where(e => e.Conversation.OrganizationId == organizationId);

    public async Task<InsightsStats> GetSummaryStatsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DateRangeSelector dateRangeSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        return await CalculateSummaryStatsAsync(
            organization,
            roomSelector,
            dateRangeSelector,
            tagSelector,
            filter);
    }

    async Task<InsightsStats> CalculateSummaryStatsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DateRangeSelector dateRangeSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        var stateChangeQueryable = GetConversationEventsQueryable(organization.Id)
            .Apply(roomSelector)
            .Apply(tagSelector)
            .ApplyFilter(filter);
        var conversationsQueryable = GetConversationsCreatedQueryable(organization.Id)
            .Apply(roomSelector)
            .Apply(tagSelector)
            .ApplyFilter(filter);

        var respondedCount = await stateChangeQueryable
            .Apply(dateRangeSelector)
            .Where(e => e.NewState == ConversationState.Waiting) // Purposely leave out "snoozed" state from this count.
            .GroupBy(e => e.ConversationId)
            .CountAsync();

        var wentOverdueCount = await WentOverdueCountAsync(
            stateChangeQueryable,
            conversationsQueryable,
            dateRangeSelector);

        var neededAttentionCount = await GetNeededAttentionCountAsync(
            stateChangeQueryable,
            conversationsQueryable,
            dateRangeSelector);

        var openedCountQueryable = conversationsQueryable.Apply(dateRangeSelector);
        var openedCount = await openedCountQueryable.CountAsync();
        var openedConversationsInRoomsCount = await openedCountQueryable
            .GroupBy(c => c.RoomId)
            .CountAsync();

        return new InsightsStats(
            wentOverdueCount,
            openedCount,
            neededAttentionCount,
            respondedCount,
            openedConversationsInRoomsCount);
    }

    public async Task<IReadOnlyList<ConversationVolumePeriod>> GetConversationVolumeRollupsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        return await CalculateConversationVolumeRollupsAsync(
            organization,
            roomSelector,
            datePeriodSelector,
            tagSelector,
            filter);
    }

    public async Task<IReadOnlyList<RoomConversationVolume>> GetConversationVolumeByRoomAsync(Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        // Get all the managed rooms for the organization, then filter by the room selector.
        var includeAllTags = tagSelector == TagSelector.All;
        var rooms = await _db.Rooms
            .Apply(roomSelector)
            .ApplyFilter(filter, _db)
            .AsNoTracking() // Ensures filtered include works correctly in our tests. https://github.com/dotnet/efcore/issues/26482
            .Where(r => r.ManagedConversationsEnabled)
            .Where(r => r.OrganizationId == organization.Id)
            .Include(r => r.Organization)
            .Include(r => r.Customer)
            .Include(r => r.Assignments)
            .ThenInclude(a => a.Member)
            .ThenInclude(m => m.User)
            .Select(r => new {
                Room = r,
                Count = r.Conversations.Count(c =>
                    c.Created >= datePeriodSelector.StartDateTimeUtc
                    && c.Created < datePeriodSelector.EndDateTimeUtc
                    && (includeAllTags || c.Tags.Any(t => t.Tag.Name == tagSelector.Tag)))
            })
            .ToListAsync();

        return rooms.Select(r => new RoomConversationVolume(r.Room, r.Count))
            .OrderByDescending(r => r.OpenConversationCount)
            .ToList();
    }

    public async Task<IReadOnlyList<ResponderConversationVolume>> GetConversationVolumeByResponderAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        // Activity is any message posted in a managed room by someone from the "host" organization.
        var responderCounts = await _db.ConversationEvents
            .AsNoTracking()
            .ApplyFilter(filter)
            .OfType<MessagePostedEvent>()
            .Apply(roomSelector)
            .Apply(datePeriodSelector)
            .Apply(tagSelector)
            .Include(c => c.Member)
            .ThenInclude(m => m.RoomAssignments.Where(a => a.Role == RoomRole.FirstResponder))
            .ThenInclude(a => a.Room) // We need to get the name of the rooms where member is a first responder.
            .Include(c => c.Member)
            .ThenInclude(m => m.User)
            .Where(e => e.Conversation.Room.ManagedConversationsEnabled         // Room must be managed.
                && e.Conversation.Room.OrganizationId == organization.Id        // And in the current organization
                && e.Member.OrganizationId == organization.Id                   // Message posted by someone in this org.
                && !e.Member.IsGuest)                                           // But not a guest
            .GroupBy(e => e.MemberId)
            .Select(g => new {
                g.First().Member,
                Count = g.Select(x => x.ConversationId).Distinct().Count()
            })
            .ToListAsync();

        return responderCounts.Select(m => new ResponderConversationVolume(m.Member, m.Count))
            .OrderByDescending(u => u.OpenConversationCount)
            .ToList();
    }

    async Task<IReadOnlyList<ConversationVolumePeriod>> CalculateConversationVolumeRollupsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        var conversationsQueryable = GetConversationsCreatedQueryable(organization.Id)
            .Apply(roomSelector)
            .Apply(tagSelector)
            .ApplyFilter(filter);
        var conversationsDateRangeQueryable = conversationsQueryable.Apply(datePeriodSelector);

        var (alreadyOpenCount, alreadyOverdueCount) = await GetOpenAndOverdueCountsBeforeDateAsync(
                organization,
                roomSelector,
                tagSelector,
                datePeriodSelector.StartDateTimeUtc);

        // Conversations that were open before the date range, and never had a state change, are still open.
        // The count of conversations created on every day. This one is straight forward.
        var newCounts = await datePeriodSelector
            .GroupByDay(conversationsDateRangeQueryable)
            .OrderBy(g => g.Key)
            .Select(g => new {
                Date = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(g => LocalDate.FromDateTime(g.Date), g => g.Count);

        var eventsQueryable = GetConversationEventsQueryable(organization.Id)
            .Apply(datePeriodSelector)
            .Apply(roomSelector)
            .Apply(tagSelector);

        var stateChangesQueryable = eventsQueryable.Where(e =>
            // Ignore state changes from archived to closed and vice versa.
            !(ConversationExtensions.NotOpenStates.Contains(e.NewState) &&
              ConversationExtensions.NotOpenStates.Contains(e.OldState)));

        // DATABASE CALL HERE!
        // UGH! We can't run the following query on the server so we bring all the data here and do it.
        var stateChanges = await stateChangesQueryable.ToListAsync();

        var stateChangesByDay = datePeriodSelector
            .GroupByLocalDate(stateChanges)
            .ToReadOnlyOrderedList();

        // Keeps the ugly TryGetValue out of the final tabulation.
        int GetNewCount(LocalDate day) => newCounts.TryGetValue(day, out var count)
            ? count
            : 0;

        var openedCounts = stateChangesByDay
            .AggregateGroups(
                dayEvents => {
                    var reOpened = dayEvents
                        .GroupBy(e => e.ConversationId)
                        .Count(WhereReopenedDuringPeriod); // Count re-opened conversations, but not ones that were already open (entering this period), closed in this period, then re-opened..
                    var newCount = GetNewCount(dayEvents.Key); // We don't have a state change event for new conversations.
                    return reOpened + newCount;
                },
                dayEvents =>
                    -1 * GetConversationsClosedDuringPeriodAtPeriodEndCount(dayEvents), // Count it if it's not open.
                alreadyOpenCount
            )
            .ToDictionary(g => g.Key, g => g.Value);

        var overdueCounts = stateChangesByDay
            .AggregateGroups(
                dayEvents => dayEvents
                    .GroupBy(e => e.ConversationId)
                    .Count(WhereNotAlreadyOverdueAndWentOverdueDuringPeriod),
                dayEvents => -1 * GetOverdueConversationsNoLongerOverdueCount(dayEvents),
                alreadyOverdueCount)
            .ToDictionary(g => g.Key, g => g.Value);


        // The number of open conversations on a given day.
        int GetOpenCount(LocalDate day) => openedCounts.TryGetValue(day, out var count) ? count : 0;

        // The number of conversations that were already overdue before the time period is all conversations where the
        // last state change before the start of the period was the Overdue state.
        int GetOverdueCount(LocalDate day) => overdueCounts.TryGetValue(day, out var value) ? value : 0;

        return datePeriodSelector.EnumerateDays()
            .Select(day => new ConversationVolumePeriod(
                day,
                GetOverdueCount(day),
                GetNewCount(day),
                GetOpenCount(day)))
            .ToReadOnlyList();
    }

    public async Task<IReadOnlyList<RoomOption>> GetRoomFilterList(Organization organization)
    {
        var rooms = await _db.Rooms
            .Where(r => r.OrganizationId == organization.Id)
            .Where(r => r.ManagedConversationsEnabled || r.Conversations.Any())
            .Select(r => new { r.Name, r.PlatformRoomId }) // We do a projection to avoid sending all the data.
            .ToListAsync();
        return rooms.Select(r => new RoomOption(r.Name ?? r.PlatformRoomId, r.PlatformRoomId)).ToReadOnlyList();
    }


    public async Task<Conversation?> GetFirstConversationAsync(Organization organization, RoomSelector roomSelector)
    {
        return await _db.Conversations.Where(c => c.OrganizationId == organization.Id)
            .Apply(roomSelector)
            .OrderBy(c => c.Created)
            .FirstOrDefaultAsync();
    }

    public async Task<Dictionary<ConversationLinkType, int>> GetCreatedTicketCountsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        var queryable = _db.Conversations.Where(c => c.OrganizationId == organization.Id)
            .Apply(roomSelector)
            .Apply(datePeriodSelector)
            .Apply(tagSelector)
            .ApplyFilter(filter);

        return await queryable
            .SelectMany(c => c.Links)
            .Where(l => l.OrganizationId == organization.Id)
            .GroupBy(l => l.LinkType)
            .Select(l => new { l.Key, Count = l.Count() })
            .ToDictionaryAsync(l => l.Key, l => l.Count);
    }

    public async Task<IReadOnlyList<TagFrequency>> GetTagFrequencyAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        var queryable = _db.Conversations
            .Include(c => c.Tags)
            .ThenInclude(ct => ct.Tag)
            .Where(c => c.OrganizationId == organization.Id)
            .Apply(roomSelector)
            .Apply(datePeriodSelector)
            .Apply(tagSelector)
            .ApplyFilter(filter);
        var tags = await queryable
            .SelectMany(c => c.Tags)
            .Where(TagRepository.VisibleConversationTagsExpression)
            .ToListAsync();

        return tags
            .GroupBy(t => t.Tag, t => t.TagId)
            .Select(g => new TagFrequency(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToReadOnlyList();
    }

    // The number of conversations that were overdue any time in the period but ended the period no longer overdue.
    internal static int GetOverdueConversationsNoLongerOverdueCount(
        IEnumerable<StateChangedEvent> periodEvents)
    {
        return periodEvents
            .Where(e => e.NewState != ConversationState.Unknown) // Ignore unknown state changes
            .OrderBy(d => d.Created)
            .GroupBy(c => c.ConversationId)
            .Count(WhereOverdueInPeriodButEndedNotOverdue);
    }

    // The number of conversations that started the period open, but ended the period closed.
    internal static int GetConversationsClosedDuringPeriodAtPeriodEndCount(IEnumerable<StateChangedEvent> periodEvents)
    {
        return periodEvents
            .Where(e => e.NewState != ConversationState.Unknown) // Ignore unknown state changes
            .OrderByDescending(d => d.Created) // We need the most recent state change, this ordering helps us get that.
            .GroupBy(c => c.ConversationId)    // We're counting conversations, not events.
            .Select(c => c.First())            // Grab the most recent state change in each group.
            .Count(c => !c.NewState.IsOpen());  // Make sure it meets the criteria.
    }

    /// <summary>
    /// Returns whether this group of state events represents a conversation that was not already open in this
    /// time period and then was reopened during this time period.
    /// </summary>
    /// <remarks>
    /// If the conversation was open in the previous period, then closed and then reopened in this period,
    /// it should not be counted.
    /// </remarks>
    internal static bool WhereReopenedDuringPeriod(IEnumerable<StateChangedEvent> conversationStateChangesInPeriod)
    {
        // ReSharper disable once PossibleMultipleEnumeration
#pragma warning disable CA1851
        var first = conversationStateChangesInPeriod.FirstOrDefault();
        if (first is null)
        {
            return false;
        }

        // ReSharper disable once PossibleMultipleEnumeration
        return !first.OldState.IsOpen() && conversationStateChangesInPeriod.Any(state => state.NewState.IsOpen());
#pragma warning restore CA1851
    }

    /// <summary>
    /// Returns whether this group of state events represents a conversation that was not already overdue in this
    /// time period and then went overdue during this time period.
    /// </summary>
    /// <remarks>
    /// If the conversation was overdue in the previous period, then not overdue and then became overdue in this period,
    /// it should not be counted.
    /// </remarks>
    internal static bool WhereNotAlreadyOverdueAndWentOverdueDuringPeriod(IEnumerable<StateChangedEvent> conversationStateChangesInPeriod)
    {
#pragma warning disable CA1851
        // ReSharper disable once PossibleMultipleEnumeration
        var first = conversationStateChangesInPeriod.FirstOrDefault();
        if (first is null)
        {
            return false;
        }

        return first.OldState != ConversationState.Overdue
               // ReSharper disable once PossibleMultipleEnumeration
               && conversationStateChangesInPeriod.Any(state => state.NewState == ConversationState.Overdue);
#pragma warning restore CA1851
    }

    /// <summary>
    /// Returns whether this group of state events represents a conversation that was overdue any time in this period
    /// but ended the period not overdue.
    /// </summary>
    /// <remarks>
    /// This is used to help us figure out the number of conversations in a period that should no longer be counted
    /// in the next period.
    /// </remarks>
    internal static bool WhereOverdueInPeriodButEndedNotOverdue(
        IEnumerable<StateChangedEvent> conversationStateChangesInPeriod)
    {
        // Either we were never overdue OR we were overdue and then we were not overdue.
        var last = conversationStateChangesInPeriod
            .LastOrDefault(e => e.OldState == ConversationState.Overdue || e.NewState == ConversationState.Overdue);
        return last is not null && last.OldState == ConversationState.Overdue;
    }

    internal async Task<(int, int)> GetOpenAndOverdueCountsBeforeDateAsync(
        Organization organization,
        RoomSelector roomSelector,
        TagSelector tagSelector,
        DateTime dateTimeUtc)
    {
        var conversationsQueryable = GetConversationsCreatedQueryable(organization.Id)
            .Apply(roomSelector)
            .Apply(tagSelector)
            .Where(c => c.Created < dateTimeUtc);
        var lastStateChangeQueryable = conversationsQueryable
            .SelectMany(LastStateChangeForConversationBeforeDate(dateTimeUtc));                             // Get the last occurring state change (due to sort).

        var openedBeforeNoStateChangesQueryable = conversationsQueryable
            .Where(ConversationHasNoStateChangesBeforeTheDate(dateTimeUtc));
        var openedBeforeNoStateChanges = await openedBeforeNoStateChangesQueryable
            .CountAsync();
        var openedCount = openedBeforeNoStateChanges + await lastStateChangeQueryable
            .Where(e => e.NewState != ConversationState.Archived && e.NewState != ConversationState.Closed) // Where the last event was still open.
            .CountAsync();
        var overdueCount = await lastStateChangeQueryable
            .Where(e => e.NewState == ConversationState.Overdue) // Where the last event was going overdue.
            .CountAsync();

        return (openedCount, overdueCount);
    }

    static Expression<Func<Conversation, IEnumerable<StateChangedEvent>>> LastStateChangeForConversationBeforeDate(DateTime dateTimeUtc)
    {
        return c => c.Events.OfType<StateChangedEvent>()
            .Where(se => se.Created < dateTimeUtc) // Only look at events prior to the instant.
            .OrderByDescending(e => e.Created)     // Sort in descending order so we can easily get the last occurring event.
            .Take(1);
    }

    static Expression<Func<Conversation, bool>> ConversationHasNoStateChangesBeforeTheDate(DateTime dateTimeUtc)
    {
        return c => !c.Events.OfType<StateChangedEvent>().Any(e => e.Created < dateTimeUtc);
    }

    static async Task<int> GetNeededAttentionCountAsync(
        IQueryable<StateChangedEvent> queryable,
        IQueryable<Conversation> conversationsQueryable,
        DateRangeSelector dateRangeSelector)
    {
        var neededAttentionStateChangeQueryable = queryable.Apply(dateRangeSelector)
            .Where(e => e.NewState == ConversationState.NeedsResponse
                || ConversationExtensions.WaitingForResponseStates.Contains(e.OldState));

        var conversationNoStateChangesQueryable = conversationsQueryable // All relevant conversations
            .Where(c => !c.Events.OfType<StateChangedEvent>().Any());   // where the conversation has no state changes.

        // Needed Attention count is the sum of:
        // - Conversation Events that moved to or from the Needed Attention state in the time period.
        var neededAttentionStateChange = await neededAttentionStateChangeQueryable
            .Select(e => e.ConversationId)
            .Distinct()
            .CountAsync();
        // - All Conversations that were created with no state changes (they're still open).
        var conversationsNoStateChanges = await conversationNoStateChangesQueryable
            .CountAsync();

        return neededAttentionStateChange + conversationsNoStateChanges;
    }

    static async Task<int> WentOverdueCountAsync(
        IQueryable<StateChangedEvent> stateChangeQueryable,
        IQueryable<Conversation> conversationsQueryable,
        DateRangeSelector dateRangeSelector)
    {
        // Overdue count is the sum of:
        // - Conversation Events that moved to or from the Overdue state in the time period.
        var wentOverdueQueryable = stateChangeQueryable.Apply(dateRangeSelector)
            .Where(e => e.NewState == ConversationState.Overdue || e.OldState == ConversationState.Overdue);

        // - Conversation events that moved to the Overdue state and never moved again prior to the time period.
        var wentAndStayedOverduePriorCount = await conversationsQueryable
            .SelectMany(LastStateChangeForConversation)
            .Where(e => e.NewState == ConversationState.Overdue)                     // Where the last event was going overdue.
            .CountAsync(last => last.Created < dateRangeSelector.StartDateTimeUtc);  // And it went overdue prior to the period.

        var wentOverdueDuringCount = await wentOverdueQueryable
            .Select(e => e.ConversationId)
            .Distinct()
            .CountAsync();

        return wentAndStayedOverduePriorCount + wentOverdueDuringCount;
    }

    // Expression to query the last state change for for a conversation.
    static Expression<Func<Conversation, IEnumerable<StateChangedEvent>>> LastStateChangeForConversation =>
        c => c.Events.OfType<StateChangedEvent>().OrderByDescending(e => e.Created).Take(1);
}
