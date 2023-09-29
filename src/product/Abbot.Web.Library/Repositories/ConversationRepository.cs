using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using Segment;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Integrations;
using Serious.Abbot.Serialization;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using Serious.Filters;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Repositories;

public class ConversationRepository : IConversationRepository
{
    static readonly ILogger<ConversationRepository> Log =
        ApplicationLoggerFactory.CreateLogger<ConversationRepository>();

    readonly AbbotContext _db;
    readonly IConversationPublisher _conversationPublisher;
    readonly CoverageHoursResponseTimeCalculator _coverageHoursResponseTimeCalculator;
    readonly IAnalyticsClient _analyticsClient;
    readonly IAuditLog _auditLog;
    readonly IClock _clock;

    public ConversationRepository(
        AbbotContext db,
        IConversationPublisher conversationPublisher,
        CoverageHoursResponseTimeCalculator coverageHoursResponseTimeCalculator,
        IAnalyticsClient analyticsClient,
        IAuditLog auditLog,
        IClock clock)
    {
        _db = db;
        _conversationPublisher = conversationPublisher;
        _coverageHoursResponseTimeCalculator = coverageHoursResponseTimeCalculator;
        _analyticsClient = analyticsClient;
        _auditLog = auditLog;
        _clock = clock;
    }

    IQueryable<Conversation> Entities => _db.Conversations
        .Include(c => c.Assignees)
        .ThenInclude(a => a.User)
        .Include(c => c.Tags)
        .ThenInclude(t => t.Tag)
        .Include(c => c.Links)
        .Include(c => c.StartedBy.Organization)
        .Include(c => c.StartedBy.User)
        .Include(c => c.Members)
        .ThenInclude(m => m.Member.User)
        .Include(c => c.Members)
        .ThenInclude(m => m.Member.Organization)
        .Include(c => c.Room.Customer)
        .Include(c => c.Room.Customer!.TagAssignments)
        .ThenInclude(a => a.Tag)
        .Include(c => c.Room.Links)
        .Include(c => c.Room.Organization)
        .Include(c => c.Room.Assignments)
        .ThenInclude(ra => ra.Member.User)
        .Include(c => c.Hub)
        .Include(c => c.Room.Metadata)
        .ThenInclude(m => m.MetadataField);

    public async Task<IReadOnlyList<Conversation>> GetRecentActiveConversationsAsync(Room room, TimeSpan timeSpan)
    {
        if (timeSpan < TimeSpan.Zero)
        {
            throw new ArgumentException("Time span must be positive.", nameof(timeSpan));
        }
        var lastConversationDate = _clock.UtcNow.Subtract(timeSpan);

        return await Entities
            .Include(c => c.Events)
            .Include(c => c.Tags)
            .ThenInclude(t => t.Tag)
            .Where(c => c.RoomId == room.Id)
            .Where(c => c.State != ConversationState.Archived)
            .Where(c => c.LastMessagePostedOn > lastConversationDate || c.Created > lastConversationDate)
            .OrderByDescending(c => c.LastMessagePostedOn)
            .ThenBy(c => c.Created)
            .ToListAsync();
    }

    public async Task<bool> HasAnyConversationsAsync(Organization organization)
    {
        return await _db.Conversations.AnyAsync(c => c.OrganizationId == organization.Id);
    }

    public async Task<Conversation?> GetConversationByThreadIdAsync(
        string threadId,
        Id<Room> roomId,
        bool followHubThread = false,
        CancellationToken cancellationToken = default) => await Entities.SingleOrDefaultAsync(
            c => (c.RoomId == roomId.Value && c.ThreadIds.Contains(threadId))
                || (followHubThread && c.Hub!.RoomId == roomId.Value && c.HubThreadId == threadId),
            cancellationToken);

    public async Task<IReadOnlyList<EntityResult<Conversation>>> GetConversationsByThreadIdsAsync(
        IEnumerable<string> threadIds,
        Organization organization)
    {
        var conversations = await Entities
            .Where(r => r.OrganizationId == organization.Id)
            .Where(r => threadIds.Contains(r.FirstMessageId))
            .ToDictionaryAsync(t => t.FirstMessageId);

        EntityResult<Conversation> GatherResult(string firstMessageId)
        {
            return conversations.GetValueOrDefault(firstMessageId);
        }

        return threadIds.Select(GatherResult).ToList();
    }

    public async Task<Conversation?> GetConversationAsync(Id<Conversation> id) => await GetConversationAsync(id.Value);

    public async Task<int> BulkCloseOrArchiveConversationsAsync(
        IEnumerable<Id<Conversation>> ids,
        ConversationState newState,
        Id<Organization> organizationId,
        Member actor,
        string source)
    {
        var conversationIds = ids.Select(id => id.Value).ToList();
        var conversations = await Entities
            .Where(c => conversationIds.Contains(c.Id))
            .Where(c => c.OrganizationId == organizationId.Value)
            .ToListAsync();

        foreach (var conversation in conversations)
        {
            await (newState switch
            {
                ConversationState.Closed => CloseAsync(conversation, actor, _clock.UtcNow, source),
                ConversationState.Archived => ArchiveAsync(conversation, actor, _clock.UtcNow),
                _ => throw new ArgumentOutOfRangeException(nameof(newState), newState, "This method can only archive or close conversations.")
            });
        }

        return conversations.Count;
    }

    public async Task<Conversation?> GetConversationAsync(int id) =>
        await Entities.SingleOrDefaultAsync(c => c.Id == id);

    public virtual async Task<ConversationListWithStats> QueryConversationsWithStatsAsync(
        ConversationQuery query,
        DateTime nowUtc,
        int pageNumber,
        int pageSize) => await QueryConversations(query, nowUtc, pageNumber, pageSize, includeStats: true);

    public async Task<IPaginatedList<Conversation>> QueryConversationsAsync(
        ConversationQuery query,
        DateTime nowUtc,
        int pageNumber,
        int pageSize)
    {
        var result = await QueryConversations(query, nowUtc, pageNumber, pageSize, includeStats: false);
        return result.Conversations;
    }

    async Task<ConversationListWithStats> QueryConversations(
        ConversationQuery query,
        DateTime nowUtc,
        int pageNumber,
        int pageSize,
        bool includeStats)
    {
        var queryable = Entities
            .Where(c => c.OrganizationId == query.OrganizationId)
            .Where(c => c.State != ConversationState.Hidden)
            .Apply(query.SuggestedTaskSelector)
            .Apply(query.RoomSelector); // Apply room filter

        queryable = query.ApplyTagFilter(queryable);

        // Compute stats without filtering by state.
        var stats = includeStats
            ? await GetConversationStatsAsync(queryable)
            : new ConversationStats(new Dictionary<ConversationState, int>(), 0);

        // Now apply the state filter if any
        queryable = query.State.Apply(queryable);

        // Finally, order the results
        queryable = query.Ordering.Apply(queryable, nowUtc);

        // Paginate!
        var paginated = await PaginatedList.CreateAsync(queryable, pageNumber, pageSize);

        return new ConversationListWithStats(paginated, stats);
    }

    public virtual async Task<IReadOnlyList<ConversationTrendsRollup>> GetDailyRollupsAsync(RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        Organization organization,
        FilterList filter)
    {
        var timezoneId = datePeriodSelector.Timezone.Id;

        var metricObservations = await _db.MetricObservations
            .Apply(roomSelector)
            .Apply(datePeriodSelector)
            .Apply(tagSelector)
            .ApplyFilter(filter)
            .Where(o => o.OrganizationId == organization.Id)
            .Where(o => new[]
                {
                    ConversationMetrics.TimeToFirstResponse,
                    ConversationMetrics.TimeToResponse,
                    ConversationMetrics.TimeToFirstResponseDuringCoverage,
                    ConversationMetrics.TimeToResponseDuringCoverage,
                    ConversationMetrics.TimeToClose,
                    ConversationMetrics.ResponseWithinTarget
                }.Contains(o.Metric)
            )
            .GroupBy(o => new {
                TimeZoneInfo.ConvertTimeBySystemTimeZoneId(o.Timestamp, timezoneId).Date,
                o.Metric,
            })
            .Select(g => new {
                g.Key.Date,
                g.Key.Metric,
                Average = g.Average(o => o.Value),
                Sum = g.Sum(o => o.Value),
                Count = g.Count()
            })
            .OrderBy(o => o.Date)
            .ToDictionaryAsync(o => (LocalDate.FromDateTime(o.Date), o.Metric));

        var createdCounts = await _db.Conversations
            .Where(c => c.OrganizationId == organization.Id)
            .Apply(roomSelector)
            .Apply(datePeriodSelector)
            .Apply(tagSelector)
            .ApplyFilter(filter)
            .GroupByDay(datePeriodSelector)
            .Select(g => new {
                Date = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(o => LocalDate.FromDateTime(o.Date));

        var rollUps = new List<ConversationTrendsRollup>();
        foreach (var date in datePeriodSelector.EnumerateDays())
        {
            TimeSpan? averageTimeToFirstResponse =
                metricObservations.TryGetValue((date, ConversationMetrics.TimeToFirstResponse), out var ttfrSeconds)
                    ? TimeSpan.FromSeconds(ttfrSeconds.Average)
                    : null;

            TimeSpan? averageTimeToResponse =
                metricObservations.TryGetValue((date, ConversationMetrics.TimeToResponse), out var ttrSeconds)
                    ? TimeSpan.FromSeconds(ttrSeconds.Average)
                    : null;

            TimeSpan? averageTimeToFirstResponseDuringCoverage =
                metricObservations.TryGetValue((date, ConversationMetrics.TimeToFirstResponseDuringCoverage), out var ttfrdcSeconds)
                    ? TimeSpan.FromSeconds(ttfrdcSeconds.Average)
                    : null;

            TimeSpan? averageTimeToResponseDuringCoverage =
                metricObservations.TryGetValue((date, ConversationMetrics.TimeToResponseDuringCoverage), out var ttrcdcSeconds)
                    ? TimeSpan.FromSeconds(ttrcdcSeconds.Average)
                    : null;

            TimeSpan? averageTimeToClose =
                metricObservations.TryGetValue((date, ConversationMetrics.TimeToClose), out var ttcSeconds)
                    ? TimeSpan.FromSeconds(ttcSeconds.Average)
                    : null;

            int? newCount = createdCounts.GetValueOrDefault(date) is { } v
                ? v.Count
                : null;

            // There's two possible cases we have here:
            // 1. Every response was past the target response time, in which case `percentWithinTarget == 0`.
            // 2. There were no responses in a room that has a target response time set `percentWithinTarget == null`.
            // That's why we set `percentWithinTarget` to `null` if `averageTimeToResponse` is `null`,
            // so we can tell these two cases apart. I could have returned -1 here too. It's a matter of taste: @haacked
            int? percentWithinTarget = metricObservations.TryGetValue((date, ConversationMetrics.ResponseWithinTarget),
                                           out var responseWithinTarget)
                                       && responseWithinTarget.Count > 0
                ? (int)(100 * responseWithinTarget.Sum / responseWithinTarget.Count)
                : null;

            var start = date.AtMidnight().ToDateTimeUnspecified();
            var end = date.At(LocalTime.MaxValue).ToDateTimeUnspecified();
            rollUps.Add(new ConversationTrendsRollup(
                start,
                end,
                averageTimeToFirstResponse,
                averageTimeToResponse,
                averageTimeToFirstResponseDuringCoverage,
                averageTimeToResponseDuringCoverage,
                averageTimeToClose,
                percentWithinTarget,
                newCount));
        }

        return rollUps;
    }

    public async Task UpdateForNewMessageAsync(
        Conversation conversation,
        MessagePostedEvent messagePostedEvent,
        ConversationMessage message,
        bool posterIsSupportee)
    {
        if (conversation.Events.OfType<MessagePostedEvent>().Any(m => m.MessageId == messagePostedEvent.MessageId))
        {
            // This isn't perfect. A race condition can still cause a duplicate to occur, but we'll just deal with it.
            // I'm not sure we can put a unique index on `MessagePostedEvent` and `MessageId` because of the
            // EF table hierarchy.
            return;
        }

        var postedBy = message.From;
        var utcTimestamp = message.UtcTimestamp;
        var threadId = messagePostedEvent.ThreadId ?? messagePostedEvent.MessageId;

        if (posterIsSupportee
            && string.CompareOrdinal(conversation.Properties.LastSupporteeMessageId, message.MessageId) <= 0)
        {
            conversation.Properties = (conversation.Properties with
            {
                LastSupporteeMessageId = message.MessageId,
            });
        }

        // Update the conversation timeline
        await QueueTimelineEventAsync(
            conversation,
            postedBy,
            utcTimestamp,
            messagePostedEvent,
            threadId);

        await UpdateConversationStateAsync(conversation, threadId, postedBy, utcTimestamp, posterIsSupportee);

        Log.MessageAddedToConversation(message.MessageId);

        await _conversationPublisher.PublishNewMessageInConversationAsync(
            conversation,
            message,
            messagePostedEvent);
    }

    public async Task UpdateSummaryAsync(
        Conversation conversation,
        string threadId,
        SummarizationResult summarizationResult,
        ConversationProperties properties,
        SlackTimestamp timestamp,
        Member actor,
        DateTime utcTimestamp)
    {
        conversation.Properties = properties;
        // Set legacy column
        conversation.Summary = properties.Summary;

        var lastMessageEvent = conversation
            .Events
            .OfType<MessagePostedEvent>()
            .OrderBy(e => e.Created)
            .LastOrDefault(e => e.MessageId == timestamp.ToString()); // Update the most recent one if duplicates.

        if (lastMessageEvent?.DeserializeMetadata() is { } lastMessageMetadata)
        {
            var newMetadata = lastMessageMetadata with
            {
                SummarizationResult = summarizationResult
            };
            lastMessageEvent.Metadata = newMetadata.ToJson();
        }

        await _db.SaveChangesAsync();
    }

    public async Task SaveThreadIdToConversationAsync(Conversation conversation, string threadId)
    {
        if (!conversation.ThreadIds.Contains(threadId))
        {
            conversation.ThreadIds.Add(threadId);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<StateChangedEvent?> SnoozeConversationAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp)
    {
        if (conversation.State is not ConversationState.Snoozed and not ConversationState.Archived)
        {
            var stateChange = await QueueTimelineEventAsync(
                conversation,
                actor,
                utcTimestamp,
                new StateChangedEvent
                {
                    OldState = conversation.State,
                    NewState = ConversationState.Snoozed,
                    Implicit = false,
                });

            conversation.State = ConversationState.Snoozed;
            await _db.SaveChangesAsync();

            await _conversationPublisher.PublishConversationStateChangedAsync(stateChange);
            return stateChange;
        }

        return null;
    }

    public async Task<StateChangedEvent?> WakeConversationAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp)
    {
        if (conversation.State is ConversationState.Snoozed)
        {
            var timeline = await GetTimelineAsync(conversation);
            var lastStateChange = timeline
                .OfType<StateChangedEvent>()
                .LastOrDefault();

            Expect.True(lastStateChange?.NewState is ConversationState.Snoozed);

            var restoredState = lastStateChange.OldState;

            ResetNotificationDates(conversation);
            var stateChange = await QueueTimelineEventAsync(
                conversation,
                actor,
                utcTimestamp,
                new StateChangedEvent
                {
                    OldState = ConversationState.Snoozed,
                    NewState = restoredState,
                    Implicit = false,
                });

            // After the snooze period is over, we move the conversation back to the waiting state.
            conversation.State = restoredState;
            await _db.SaveChangesAsync();

            await _conversationPublisher.PublishConversationStateChangedAsync(stateChange);
            return stateChange;
        }

        return null;
    }

    async Task<StateChangedEvent?> UpdateConversationStateAsync(
        Conversation conversation,
        string? threadId,
        Member postedBy,
        DateTime utcTimestamp,
        bool posterIsSupportee)
    {
        if (conversation is { State: ConversationState.Hidden, Room.ManagedConversationsEnabled: false })
        {
            // Ignore hidden conversations in unmanaged rooms. For hidden conversations in managed rooms,
            // new messages "wake up" the conversation and trigger state changes as normal.
            return null;
        }

        // Update state for the conversation
        conversation.LastMessagePostedOn = utcTimestamp;

        await _db.Entry(conversation).Collection(c => c.Members).LoadAsync();

        // Find this user's member record, if they have one
        var member = conversation.Members.SingleOrDefault(m => m.MemberId == postedBy.Id);
        if (member is null)
        {
            // No record, create one
            conversation.Members.Add(new ConversationMember
            {
                Conversation = conversation,
                Member = postedBy,
                JoinedConversationAt = utcTimestamp,
                LastPostedAt = utcTimestamp,
            });
        }
        else
        {
            // Found a record, update timestamps
            member.LastPostedAt = utcTimestamp;
        }

        // When a supporter (aka Agent) responds to a snoozed conversation, we have to wake it up to restore
        // its original state, and then we can process the state change.
        if (!posterIsSupportee && conversation.State is ConversationState.Snoozed)
        {
            await WakeConversationAsync(conversation, postedBy, utcTimestamp);
        }

        // Evaluate state transition
        var newState = conversation.State;
        switch (conversation.State)
        {
            case ConversationState.Archived:
            case ConversationState.Closed when !posterIsSupportee:
                // Retain current state
                break;
            case ConversationState.Waiting or ConversationState.Hidden when posterIsSupportee:
                newState = ConversationState.NeedsResponse;
                break;
            case ConversationState.Closed when posterIsSupportee:
                newState = ConversationState.NeedsResponse;
                ResetNotificationDates(conversation);
                break;
            case var state when (state is ConversationState.Hidden or ConversationState.Snoozed
                                 || state.IsWaitingForResponse())
                                && !posterIsSupportee:
                // When a supporter posts a message waiting on a response, we move the conversation back to the waiting
                // state and reset notification timeouts.
                newState = ConversationState.Waiting;
                ResetNotificationDates(conversation);

                var responseTimeDuringCoverage = await _coverageHoursResponseTimeCalculator.CalculateResponseTimeAsync(
                    conversation,
                    utcTimestamp);

                if (state is ConversationState.New)
                {
                    await QueueConversationObservationAsync(
                        conversation,
                        utcTimestamp,
                        metric: ConversationMetrics.TimeToFirstResponse,
                        observation: utcTimestamp - conversation.Created);

                    await QueueConversationObservationAsync(
                        conversation,
                        utcTimestamp,
                        metric: ConversationMetrics.TimeToFirstResponseDuringCoverage,
                        observation: responseTimeDuringCoverage);
                }

                await QueueConversationObservationAsync(
                    conversation,
                    utcTimestamp,
                    metric: ConversationMetrics.TimeToResponse,
                    observation: utcTimestamp - conversation.LastStateChangeOn);

                await QueueConversationObservationAsync(
                    conversation,
                    utcTimestamp,
                    metric: ConversationMetrics.TimeToResponseDuringCoverage,
                    observation: responseTimeDuringCoverage);

                await QueueConversationObservationAsync(
                    conversation,
                    utcTimestamp,
                    metric: ConversationMetrics.ResponseWithinTarget,
                    state is not ConversationState.Overdue);

                break;
        }

        StateChangedEvent? stateChangedEvent = null;
        // If a state transition did occur, make sure we also queue a timeline event and remove pending notifications
        if (newState != conversation.State)
        {
            if (newState is ConversationState.Waiting
                or ConversationState.Snoozed
                or ConversationState.Closed
                or ConversationState.Archived)
            {
                await RemovePendingNotificationsForConversationAsync(conversation, newState);
            }

            stateChangedEvent = new StateChangedEvent
            {
                OldState = conversation.State,
                NewState = newState,
                Implicit = true
            };

            await QueueTimelineEventAsync(
                conversation,
                postedBy,
                utcTimestamp,
                stateChangedEvent,
                threadId);

            conversation.State = newState;
            conversation.LastStateChangeOn = utcTimestamp;
        }

        if (newState == ConversationState.Waiting && conversation.FirstResponseOn is null)
        {
            conversation.FirstResponseOn = utcTimestamp;
        }

        _db.Conversations.Update(conversation);
        await _db.SaveChangesAsync();

        if (stateChangedEvent is not null)
        {
            // We only want to track this state change only after we've successfully saved the state change
            // and we know a state change occurred.
            _analyticsClient.Track(
                "Conversation State Changed",
                AnalyticsFeature.Conversations,
                postedBy,
                conversation.Room.Organization,
                new {
                    state = stateChangedEvent.NewState.ToString(),
                    old_state = stateChangedEvent.OldState.ToString(),
                });

            await _conversationPublisher.PublishConversationStateChangedAsync(stateChangedEvent);
        }
        return stateChangedEvent;
    }

    async Task RemovePendingNotificationsForConversationAsync(Conversation conversation, ConversationState newState)
    {
        try
        {
            var pendingNotifications = await _db.PendingMemberNotifications
                .Where(p => p.ConversationId == conversation.Id)
                .ToListAsync();

            _db.PendingMemberNotifications.RemoveRange(pendingNotifications);
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            Log.FailedToRemovePendingNotifications(e, conversation.Id, conversation.State, newState);
        }
    }

    static void ResetNotificationDates(Conversation conversation)
    {
        conversation.TimeToRespondWarningNotificationSent = null;
    }

    public async Task<Conversation> CreateAsync(
        Room room,
        MessagePostedEvent firstMessageEvent,
        string title,
        Member startedBy,
        DateTime startedAtUtc,
        DateTime? importedOnUtc,
        ConversationState initialState = ConversationState.New)
    {
        var conversation = new Conversation
        {
            Room = room,
            State = initialState,
            Organization = room.Organization,
            FirstMessageId = firstMessageEvent.MessageId.Require(),
            Created = startedAtUtc,
            LastStateChangeOn = startedAtUtc,
            LastMessagePostedOn = startedAtUtc,
            StartedBy = startedBy,
            ImportedOn = importedOnUtc,
            Title = title,
            Members = new List<ConversationMember>
            {
                new()
                {
                    Member = startedBy,
                    JoinedConversationAt = startedAtUtc,
                    LastPostedAt = startedAtUtc,
                }
            },
            ThreadIds = new List<string> { firstMessageEvent.MessageId }
        };

        await QueueTimelineEventAsync(conversation, startedBy, startedAtUtc, firstMessageEvent);
        await _db.Conversations.AddAsync(conversation);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException e)
            when (e.GetDatabaseError() is UniqueConstraintError
            {
                ColumnNames:
                [nameof(Conversation.RoomId), nameof(Conversation.FirstMessageId)]
            })
        {
            var existing = await _db.Conversations.SingleOrDefaultAsync(
                c => c.RoomId == conversation.RoomId
                     && c.FirstMessageId == conversation.FirstMessageId);

            if (existing is null)
            {
                throw;
            }

            _db.Entry(conversation).State = EntityState.Detached;
            conversation = existing;
            Log.DuplicateConversationCreationAttempt(e, conversation.RoomId, conversation.FirstMessageId);
            return conversation;
        }

        _analyticsClient.Track(
            "Conversation created",
            AnalyticsFeature.Conversations,
            startedBy,
            room.Organization,
            new()
            {
                { "room_is_shared", room.Shared?.ToString() ?? "null" },
                { "is_guest", startedBy.IsGuest },
                { "initial_state", initialState },
            });

        return conversation;
    }

    public async Task<IReadOnlyList<ConversationEvent>> GetTimelineAsync(Conversation conversation)
    {
        // We can't rely on `conversation.Events` being null to determine if the collection is loaded.
        // We have to check `IsLoaded`. If the collection isn't loaded, we're running a query here, which
        // doesn't check `IsLoaded`, hence we keep the `IsLoaded` check here.
        if (!conversation.Events.IsLoaded)
        {
            // Load the events if not already loaded.
            await _db.Entry(conversation)
                .Collection(r => r.Events)
                .Query()
                .OrderBy(e => e.Created)
                .ThenBy(e => e.Id)
                .Include(e => e.Member.User)
                .Include(e => e.Member.Organization)
                .Include(e => ((AttachedToHubEvent)e).Hub)
                .LoadAsync();
        }

        return conversation.Events;
    }

    public async Task<StateChangedEvent?> ArchiveAsync(Conversation conversation, Member actor, DateTime utcTimestamp)
    {
        return await ChangeStateAsync(conversation, ConversationState.Archived, actor, utcTimestamp);
    }

    public async Task<StateChangedEvent?> UnarchiveAsync(Conversation conversation, Member actor, DateTime utcTimestamp)
    {
        if (conversation.State == ConversationState.Archived)
        {
            return await ChangeStateAsync(conversation, ConversationState.Closed, actor, utcTimestamp);
        }

        return null;
    }

    public async Task<StateChangedEvent?> CloseAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp,
        string? source = null)
    {
        if (conversation.State is not (ConversationState.Closed or ConversationState.Archived))
        {
            await QueueConversationObservationAsync(conversation,
                utcTimestamp,
                ConversationMetrics.TimeToClose,
                utcTimestamp - conversation.Created);

            return await ChangeStateAsync(conversation, ConversationState.Closed, actor, utcTimestamp, source);
        }

        return null;
    }

    public async Task<StateChangedEvent?> ReopenAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp,
        ConversationState newState = ConversationState.Waiting)
    {
        if (conversation.State is ConversationState.Closed)
        {
            return await ChangeStateAsync(conversation, newState, actor, utcTimestamp);
        }

        return null;
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsInWarningPeriodForTimeToRespond(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var query = GetConversationsWaitingOnResponseQuery()
            .Where(c => c.TimeToRespondWarningNotificationSent == null)
            .Where(WarningPeriodExpression(utcNow));

        return await query.ToListAsync(cancellationToken);
    }

    public static Expression<Func<Conversation, bool>> WarningPeriodExpression(DateTime utcNow)
    {
        return c => c.Room.TimeToRespond.Warning != null
                    && utcNow - c.LastStateChangeOn >= c.Room.TimeToRespond.Warning
                    && utcNow - c.LastStateChangeOn < c.Room.TimeToRespond.Deadline
                    || (c.Room.TimeToRespond.Warning == null
                        && c.Organization.DefaultTimeToRespond.Warning != null
                        && utcNow - c.LastStateChangeOn >= c.Organization.DefaultTimeToRespond.Warning
                        && utcNow - c.LastStateChangeOn < c.Organization.DefaultTimeToRespond.Deadline);
    }

    public async Task UpdateTimeToRespondWarningNotificationSentAsync(Conversation conversation, DateTime dateSent)
    {
        conversation.TimeToRespondWarningNotificationSent = dateSent;
        await _db.SaveChangesAsync();
    }

    public async Task<StateChangedEvent?> UpdateOverdueConversationAsync(Conversation conversation, DateTime dateSent, Member actor)
    {
        if (conversation.State == ConversationState.Overdue)
        {
            return null;
        }

        var stateChange = new StateChangedEvent
        {
            OldState = conversation.State,
            NewState = ConversationState.Overdue
        };

        await QueueTimelineEventAsync(conversation, actor, dateSent, stateChange);
        conversation.State = ConversationState.Overdue;
        await _db.SaveChangesAsync();

        await _conversationPublisher.PublishConversationStateChangedAsync(stateChange);
        return stateChange;
    }

    public async Task<IReadOnlyList<Conversation>> GetOverdueConversationsToNotifyAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await GetConversationsWaitingOnResponseQuery()
            .Where(c => c.State != ConversationState.Overdue
                        && (c.Room.TimeToRespond.Deadline != null
                            && utcNow - c.LastStateChangeOn >= c.Room.TimeToRespond.Deadline
                            || (c.Room.TimeToRespond.Deadline == null
                                && c.Organization.DefaultTimeToRespond.Deadline != null
                                && utcNow - c.LastStateChangeOn >= c.Organization.DefaultTimeToRespond.Deadline)))
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationLink> CreateLinkAsync(
        Conversation conversation,
        ConversationLinkType type,
        string externalId,
        JsonSettings? settings,
        Member actor,
        DateTime utcTimestamp)
    {
        var link = new ConversationLink
        {
            Conversation = conversation,
            Organization = conversation.Organization,
            LinkType = type,
            ExternalId = externalId,
            CreatedBy = actor,
            Created = utcTimestamp,
            Settings = settings?.ToJson(),
        };

        await _db.ConversationLinks.AddAsync(link);

        var externalLinkEvent = new ExternalLinkEvent
        {
            Conversation = conversation,
            Link = link,
            Member = actor,
            Created = utcTimestamp
        };

        await QueueTimelineEventAsync(
            conversation,
            actor,
            utcTimestamp,
            externalLinkEvent);

        await _auditLog.LogConversationLinkedAsync(conversation, type, externalId, actor, conversation.Organization);
        await _db.SaveChangesAsync();

        return link;
    }

    public async Task<ConversationLink?> GetConversationLinkAsync(Id<Organization> organizationId, ConversationLinkType type,
        string externalId)
    {
        var link = await _db.ConversationLinks
            .Include(l => l.Conversation.StartedBy)
            .Include(l => l.Conversation.Room.Organization)
            .Include(l => l.Conversation.Room.Assignments)
            .ThenInclude(ra => ra.Member.User)
            .Include(l => l.Conversation.Room.Customer)
            .Include(l => l.Conversation.Room.Customer!.TagAssignments)
            .ThenInclude(a => a.Tag)
            .SingleOrDefaultAsync(l =>
                l.OrganizationId == organizationId && l.LinkType == type && l.ExternalId == externalId);

        return link;
    }

    public async Task<ConversationLink?> GetConversationLinkAsync(ConversationLinkType type, string externalId)
    {
        return await _db.ConversationLinks
            .Include(l => l.Conversation.StartedBy)
            .Include(l => l.Conversation.Room.Organization)
            .Include(l => l.Conversation.Room.Assignments)
            .ThenInclude(ra => ra.Member.User)
            .SingleOrDefaultAsync(l => l.LinkType == type && l.ExternalId == externalId);
    }

    public async Task<ConversationLink?> GetConversationLinkAsync(Id<ConversationLink> linkedConversationId)
    {
        return await _db.ConversationLinks
            .Include(l => l.Conversation.StartedBy)
            .Include(l => l.Conversation.Room.Organization)
            .Include(l => l.Conversation.Room.Assignments)
            .ThenInclude(ra => ra.Member.User)
            .SingleEntityOrDefaultAsync(linkedConversationId);
    }

    public async Task AddTimelineEventAsync(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp,
        ConversationEvent conversationEvent)
    {
        await QueueTimelineEventAsync(conversation, actor, utcTimestamp, conversationEvent);
        await _db.SaveChangesAsync();
    }

    public async Task AssignConversationAsync(Conversation conversation, IReadOnlyList<Member> assignees, Member actor)
    {
        // The UI should prevent this, but a race condition could cause an issue here.
        if (assignees.Any(a => !a.IsAgent()))
        {
            throw new InvalidOperationException("Only agents can be assigned to a conversation");
        }

        if (assignees.Count > 1)
        {
            throw new ArgumentException("Only one agent can be assigned to a conversation for now.", nameof(assignees));
        }

        conversation.Assignees.Clear();
        conversation.Assignees.AddRange(assignees);
        await _db.SaveChangesAsync();

        await _auditLog.LogConversationAssignmentAsync(
            conversation,
            assignees,
            actor);
    }

    public async Task<EntityResult> AttachConversationToHubAsync(Conversation conversation, Hub hub, string hubThreadId,
        Uri hubThreadUrl, Member actor, DateTime utcTimestamp)
    {
        if (conversation.HubId is not null)
        {
            return EntityResult.Conflict("Conversation is already attached to a hub.");
        }

        conversation.HubId = hub.Id;
        conversation.HubThreadId = hubThreadId;
        await QueueTimelineEventAsync(conversation,
            actor,
            utcTimestamp,
            new AttachedToHubEvent()
            {
                HubId = hub.Id,
                MessageId = hubThreadId,
                MessageUrl = hubThreadUrl,
            });

        await _db.SaveChangesAsync();

        await _auditLog.LogConversationAttachedAsync(conversation, hub, hubThreadId, actor);

        return EntityResult.Success();
    }

    public async Task UpdateConversationLinkAsync<TSettings>(ConversationLink conversationLink, TSettings settings)
        where TSettings : JsonSettings, ITicketingSettings
    {
        conversationLink.Settings = settings.ToJson();
        await _db.SaveChangesAsync();
    }

    IQueryable<Conversation> GetConversationsWaitingOnResponseQuery()
    {
        return _db.Conversations
            .Include(c => c.Assignees)
            .ThenInclude(a => a.User)
            .Include(c => c.Organization)
            .Include(c => c.StartedBy)
            .ThenInclude(m => m.User)
            .Include(c => c.Tags)
            .ThenInclude(t => t.Tag)
            .Include(c => c.Room.Customer!.TagAssignments)
            .ThenInclude(ta => ta.Tag)
            .Include(c => c.Room.Assignments)
            .ThenInclude(a => a.Member)
            .ThenInclude(m => m.User)
            .Where(c => c.State == ConversationState.New || c.State == ConversationState.NeedsResponse)
            .OrderBy(c => c.LastStateChangeOn); // Oldest first.
    }

    /// <summary>
    /// Performs an explicit state change on the conversation WITHOUT checking preconditions.
    /// </summary>
    async Task<StateChangedEvent?> ChangeStateAsync(
        Conversation conversation,
        ConversationState targetState,
        Member actor,
        DateTime utcTimestamp,
        string? source = null)
    {
        if (conversation.State == targetState)
        {
            return null;
        }

        var oldState = conversation.State;
        var stateChange = await QueueTimelineEventAsync(
            conversation,
            actor,
            utcTimestamp,
            new StateChangedEvent
            {
                OldState = oldState,
                NewState = targetState,
                Implicit = false
            });

        conversation.ArchivedOn = targetState == ConversationState.Archived
            ? utcTimestamp
            : null;

        conversation.ClosedOn = (conversation.State, targetState) switch
        {
            // Set the timestamp if we're moving to the closed state, but not being unarchived.
            (not ConversationState.Archived, ConversationState.Closed) => utcTimestamp,

            // Set the timestamp if we're bypassing the closed state and going straight to archived.
            (not ConversationState.Closed, ConversationState.Archived) => utcTimestamp,

            // Clear it if we're being moved away from closed, but not being archived.
            (ConversationState.Closed, not ConversationState.Archived) => null,

            // Otherwise, leave it the heck alone.
            _ => conversation.ClosedOn
        };

        conversation.State = targetState;
        conversation.LastStateChangeOn = utcTimestamp;

        _db.Conversations.Update(conversation);

        await _db.SaveChangesAsync();

        _analyticsClient.Track(
            "Conversation State Changed",
            AnalyticsFeature.Conversations,
            actor,
            conversation.Room.Organization,
            new {
                state = targetState.ToString(),
                old_state = oldState.ToString(),
                source = source ?? "unknown",
            });

        await _conversationPublisher.PublishConversationStateChangedAsync(stateChange);
        return stateChange;
    }

    /// <summary>
    /// Queues the addition of a new <see cref="ConversationEvent"/> when changes are saved.
    /// </summary>
    async ValueTask<TEvent> QueueTimelineEventAsync<TEvent>(
        Conversation conversation,
        Member actor,
        DateTime utcTimestamp,
        TEvent conversationEvent,
        string? threadId = null)
        where TEvent : ConversationEvent
    {
        conversationEvent.Conversation = conversation;
        // When we implement multi-threaded conversations, this should be the thread id where the event occurred,
        // which may be different from the conversation's first message id. For now, they're the same.
        conversationEvent.ThreadId = threadId ?? conversation.FirstMessageId;
        conversationEvent.Member = actor;
        conversationEvent.Created = utcTimestamp;

        await _db.ConversationEvents.AddAsync(conversationEvent);
        return conversationEvent;
    }

    /// <summary>
    /// Queues the addition of a new <see cref="MetricObservation"/> when changes are saved.
    /// </summary>
    Task QueueConversationObservationAsync(
        Conversation conversation,
        DateTime timestampUtc,
        string metric,
        bool observation) =>
        QueueConversationObservationAsync(conversation,
            timestampUtc,
            metric,
            observation
                ? 1
                : 0);

    /// <summary>
    /// Queues the addition of a new <see cref="MetricObservation"/> when changes are saved.
    /// </summary>
    Task QueueConversationObservationAsync(
        Conversation conversation,
        DateTime timestampUtc,
        string metric,
        TimeSpan observation) =>
        QueueConversationObservationAsync(conversation, timestampUtc, metric, observation.TotalSeconds);

    /// <summary>
    /// Queues the addition of a new <see cref="MetricObservation"/> when changes are saved.
    /// </summary>
    async Task QueueConversationObservationAsync(
        Conversation conversation,
        DateTime timestampUtc,
        string metric,
        double observation)
    {
        await _db.MetricObservations.AddAsync(new MetricObservation(
            timestampUtc,
            metric,
            conversation.Id,
            conversation.RoomId,
            conversation.OrganizationId,
            observation));
    }

    static async Task<ConversationStats> GetConversationStatsAsync(IQueryable<Conversation> query)
    {
        // Get counts by conversation state
        var counts = await query
            .GroupBy(c => c.State)
            .Select(g => new {
                State = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(g => g.State, g => g.Count);

        var totalCount = counts.Values.Sum();
        return new ConversationStats(counts, totalCount);
    }
}

public static partial class ConversationRepositoryLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Duplicate conversation creation attempt {RoomId} {FirstMessageId}")]
    public static partial void DuplicateConversationCreationAttempt(
        this ILogger<ConversationRepository> logger,
        Exception ex,
        int roomId,
        string firstMessageId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Message '{MessageId}' added to conversation")]
    public static partial void MessageAddedToConversation(
        this ILogger<ConversationRepository> logger,
        string messageId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Failed to remove pending notifications for conversation {ConversationId} in state {ConversationState} while transitioning to {NewState}.")]
    public static partial void FailedToRemovePendingNotifications(
        this ILogger<ConversationRepository> logger,
        Exception e,
        int conversationId,
        ConversationState conversationState,
        ConversationState newState);
}
