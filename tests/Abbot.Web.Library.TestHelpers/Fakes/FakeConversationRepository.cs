using Segment;
using Serious;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;
using Serious.Filters;

namespace Abbot.Common.TestHelpers.Fakes;

// Some IConversationRepository methods really need to be stubbed out.
// This allows us to do that.
public class FakeConversationRepository : ConversationRepository
{
    public ConversationListWithStats? FakeQueryResults { get; set; }
    public IReadOnlyList<ConversationTrendsRollup>? FakeDailyRollups { get; set; }
    public IList<(ConversationQuery Query, DateTime NowUtc, int PageNumber, int PageSize)> QueryCalls { get; } = new List<(ConversationQuery Query, DateTime NowUtc, int PageNumber, int PageSize)>();

    public IList<(RoomSelector RoomSelector, DatePeriodSelector DatePeriodSelector, Organization Organization)>
        DailyRollupCalls
    { get; } =
        new List<(RoomSelector RoomSelector, DatePeriodSelector DatePeriodSelector, Organization Organization)>();

    public FakeConversationRepository(
        AbbotContext db,
        IConversationPublisher conversationPublisher,
        IAnalyticsClient analyticsClient,
        IAuditLog auditLog,
        IClock clock)
        : base(
            db,
            conversationPublisher,
            new CoverageHoursResponseTimeCalculator(new UserRepository(db, clock)),
            analyticsClient,
            auditLog,
            clock)
    {
    }

    public override async Task<ConversationListWithStats> QueryConversationsWithStatsAsync(ConversationQuery query, DateTime nowUtc, int pageNumber, int pageSize)
    {
        QueryCalls.Add((query, nowUtc, pageNumber, pageSize));
        if (FakeQueryResults is not null)
        {
            return FakeQueryResults;
        }

        return await base.QueryConversationsWithStatsAsync(query, nowUtc, pageNumber, pageSize);
    }

    public override async Task<IReadOnlyList<ConversationTrendsRollup>> GetDailyRollupsAsync(RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector, TagSelector tagSelector, Organization organization, FilterList filter)
    {
        DailyRollupCalls.Add((roomSelector, datePeriodSelector, organization));
        if (FakeDailyRollups is not null)
        {
            return FakeDailyRollups;
        }

        return await base.GetDailyRollupsAsync(roomSelector, datePeriodSelector, tagSelector, organization, filter);
    }
}
