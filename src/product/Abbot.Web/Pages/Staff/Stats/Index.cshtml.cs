using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Models;
using Serious.Abbot.Security;
using Stripe;

namespace Serious.Abbot.Pages.Staff.Stats;

public class IndexPageModel : StaffToolsPage
{
    // HACK: Temporary way to identify our own organizations
    static readonly int[] SeriousOrganizations = { 1, 21, 35, 51, 58, 158 };

    readonly AbbotContext _db;

    public IndexPageModel(AbbotContext db)
    {
        _db = db;
    }

    public IDictionary<string, int> MaxIds { get; } = new Dictionary<string, int>();

    public int Take { get; private set; }
    public double DailyActiveUsers { get; private set; }
    public double DauLastWeek { get; private set; }
    public double DauMonthAgo { get; private set; }
    public int MonthlyActiveUsers { get; private set; }
    public int PreviousMonthlyActiveUsers { get; set; }

    public int TotalAgentCount { get; set; }

    public decimal MonthlyRecurringRevenue { get; private set; }
    public IEnumerable<DailyMetricsRollup> Metrics { get; private set; } = null!;

    public IDictionary<PlatformType, int> PlatformOrganizationCounts { get; private set; } = null!;

    public IDictionary<PlanType, int> OrganizationByPlanCounts { get; private set; } = null!;

    public IDictionary<DateTime, int> OrganizationCountByMonth { get; private set; } = null!;

    public IDictionary<DateTime, int> AuditLogInteractionsByMonth { get; private set; } = null!;

    public IDictionary<DateTime, int> UsersCountByMonth { get; private set; } = null!;

    public IDictionary<DateTime, int> ActiveUsersCountByMonth { get; private set; } = null!;

    public IDictionary<DateTime, int> AgentsCountByMonth { get; private set; } = null!;

    public IDictionary<DateTime, int> SkillCountByMonth { get; private set; } = null!;

    public IDictionary<PlatformType, int> PlatformInteractionCounts { get; private set; } = null!;

    public IDictionary<CodeLanguage, int> SkillByLanguageCounts { get; private set; } = null!;

    public IDictionary<DateTime, int> AnnouncementsByMonth { get; private set; } = null!;

    public IDictionary<DateTime, int> TaggedConversationsByMonth { get; private set; } = null!;

    public IList<(ConversationState, int)> ConversationStateCounts { get; private set; } = null!;

    public Metric Conversations { get; private set; } = null!;

    public int AuditLogInteractionsPast7Days { get; private set; }
    public int AuditLogInteractionsPrevious7Days { get; private set; }

    public int AuditLogInteractionsPast30Days { get; private set; }
    public int AuditLogInteractionsPrevious30Days { get; private set; }

    public double AverageMonthlyAuditLogInteractions { get; private set; }

    public int TotalInteractionCount { get; private set; }

    public int OrganizationsWithCustomSkillsCount { get; private set; }

    public double AverageNumberOfSkillsPerOrganizationNotUs { get; private set; }

    public int TotalUsersCount { get; private set; }

    public int TotalOrganizationsCount { get; private set; }

    public int TotalSkillsCount { get; private set; }
    public int TotalAnnouncementsCount { get; private set; }

    public int TotalUsersWithLocation { get; private set; }

    public int TotalPermissionsCount { get; private set; }

    public int TotalOrganizationsWithDefaultResponseTimes { get; private set; }

    public int TotalOrganizationsWithDefaultFirstResponders { get; private set; }

    public int TotalOrganizationsWithDefaultEscalationResponders { get; private set; }

    public int TotalOrganizationsWithResponseTimes { get; private set; }

    public int TotalOrganizationsWithFirstResponders { get; private set; }

    public int TotalOrganizationsWithEscalationResponders { get; private set; }

    public int TotalTaggedConversationsCount { get; private set; }

    public int TotalAITaggedConversationsCount { get; private set; }

    public IDictionary<DateTime, int> TaggedAIConversationsByMonth { get; set; } = null!;

    public IDictionary<DateTime, int> TaggedCustomersByMonth { get; set; } = null!;

    public int TotalTaggedCustomersCount { get; set; }

    public async Task<IActionResult> OnGetAsync(string? data = null, string? take = null)
    {
        Metrics = await _db.DailyMetricsRollups
            .OrderByDescending(m => m.Date)
            .ToListAsync();

        Take = int.TryParse(take, out var takeCount) ? takeCount : 30;

        if (data is "dau")
        {
            return GetMetricsCsvResult(Take);
        }

        DailyActiveUsers = Metrics.Take(7).Average(m => m.ActiveUserCount);
        DauLastWeek = Metrics.Skip(7).Take(7).Average(m => m.ActiveUserCount);
        DauMonthAgo = Metrics.Skip(35).Take(7).Average(m => m.ActiveUserCount);
        var today = DateTime.UtcNow.AddDays(-1).Date;
        var thirtyDaysAgo = today.AddDays(-30);

        MonthlyActiveUsers = await GetHumanAuditEvents()
            .Where(e => e.Created >= thirtyDaysAgo && e.Created < today)
            .GroupBy(e => e.ActorId)
            .Select(e => e.Key)
            .CountAsync();

        ActiveUsersCountByMonth = await GetHumanAuditEvents()
            .GroupBy(o => new { o.Created.Year, o.Created.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(o => new { Date = new DateTime(o.Key.Year, o.Key.Month, 1), Count = o.Select(e => e.ActorId).Distinct().Count() })
            .ToDictionaryAsync(o => o.Date, o => o.Count);

        PreviousMonthlyActiveUsers = await GetHumanAuditEvents()
            .Where(e => e.Created >= thirtyDaysAgo.AddDays(-30) && e.Created < thirtyDaysAgo)
            .GroupBy(e => e.ActorId)
            .Select(e => e.Key)
            .CountAsync();

        PlatformOrganizationCounts = await GetOrganizationsQueryable()
            .GroupBy(o => o.PlatformType)
            .Select(o => new { o.Key, Count = o.Count() })
            .ToDictionaryAsync(o => o.Key, o => o.Count);

        OrganizationByPlanCounts = await GetOrganizationsQueryable()
            .GroupBy(o => o.PlanType)
            .Select(o => new { o.Key, Count = o.Count() })
            .ToDictionaryAsync(o => o.Key, o => o.Count);

        TotalOrganizationsCount = await GetOrganizationsQueryable().CountAsync();

        TotalOrganizationsWithDefaultResponseTimes = await GetOrganizationsQueryable()
            .Where(o => o.DefaultTimeToRespond.Warning != null || o.DefaultTimeToRespond.Deadline != null)
            .CountAsync();

        TotalOrganizationsWithDefaultFirstResponders = await GetMembersQueryable()
            .Where(m => m.IsDefaultFirstResponder)
            .Where(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .GroupBy(m => m.OrganizationId)
            .CountAsync();

        TotalOrganizationsWithDefaultEscalationResponders = await GetMembersQueryable()
            .Where(m => m.IsDefaultEscalationResponder)
            .Where(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .GroupBy(m => m.OrganizationId)
            .CountAsync();

        TotalOrganizationsWithResponseTimes = await GetOrganizationsQueryable()
            .Where(o => o.Rooms!.Any(r => r.TimeToRespond.Warning != null || r.TimeToRespond.Deadline != null))
            .CountAsync();

        TotalOrganizationsWithFirstResponders = await _db.RoomAssignments
            .Where(a => a.Role == RoomRole.FirstResponder)
            .Where(a => a.Member.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .GroupBy(a => a.Room.OrganizationId)
            .CountAsync();

        TotalOrganizationsWithEscalationResponders = await _db.RoomAssignments
            .Where(a => a.Role == RoomRole.EscalationResponder)
            .Where(a => a.Member.MemberRoles.Any(r => r.Role.Name == Roles.Agent))
            .GroupBy(a => a.Room.OrganizationId)
            .CountAsync();

        OrganizationCountByMonth = CountMetricByMonthIncludeUs(Metrics, o => o.OrganizationCreatedCount);

        AuditLogInteractionsByMonth = CountMetricByMonthIncludeUs(Metrics, o => o.InteractionCount);

        var usersQueryable = _db.Users.Where(u => u.NameIdentifier != null);
        TotalUsersCount = await usersQueryable.CountAsync();

        UsersCountByMonth = CountMetricByMonthIncludeUs(Metrics, o => o.UserCreatedCount);

        AuditLogInteractionsPast7Days = Metrics
            .Take(7)
            .Sum(e => e.InteractionCount);
        AuditLogInteractionsPrevious7Days = Metrics
            .Skip(7)
            .Take(7)
            .Sum(e => e.InteractionCount);

        AuditLogInteractionsPast30Days = Metrics
            .Take(30)
            .Sum(e => e.InteractionCount);
        AuditLogInteractionsPrevious30Days = Metrics
            .Skip(30)
            .Take(30)
            .Sum(e => e.InteractionCount);

        AverageMonthlyAuditLogInteractions = await GetHumanAuditEvents()
            .GroupBy(e => new { e.Created.Year, e.Created.Month })
            .Select(e => e.Count())
            .AverageAsync();

        TotalInteractionCount = Metrics.Sum(m => m.InteractionCount);

        PlatformInteractionCounts = await GetHumanAuditEvents()
            .Include(e => e.Organization)
            .GroupBy(e => e.Organization.PlatformType)
            .Select(o => new { o.Key, Count = o.Count() })
            .ToDictionaryAsync(o => o.Key, o => o.Count);

        SkillByLanguageCounts = await GetSkillsQueryable()
            .Where(s => s.SourcePackageVersionId == null && !SeriousOrganizations.Contains(s.OrganizationId))
            .GroupBy(s => s.Language)
            .Select(s => new { s.Key, Count = s.Count() })
            .ToDictionaryAsync(s => s.Key, s => s.Count);

        OrganizationsWithCustomSkillsCount = await GetSkillsQueryable().Select(s => s.OrganizationId).Distinct().CountAsync();

        var skillsQuery = GetSkillsQueryable()
            .Where(s => !SeriousOrganizations.Contains(s.OrganizationId))
            .GroupBy(s => s.OrganizationId)
            .Select(o => new { Count = o.Count() });

        // this blows up if there are no elements, which is likely if we're running in dev mode with an empty database, so check first
        if (skillsQuery.Any())
        {
            AverageNumberOfSkillsPerOrganizationNotUs = await skillsQuery.AverageAsync(o => o.Count);
        }

        TotalSkillsCount = await GetSkillsQueryable().CountAsync();
        SkillCountByMonth = CountMetricByMonthIncludeUs(Metrics, m => m.SkillCreatedCount);

        var locationSetQueryable = GetMembersQueryable()
            .Where(s => !SeriousOrganizations.Contains(s.OrganizationId))
            .Where(m => m.Location != null);

        TotalUsersWithLocation = await locationSetQueryable.CountAsync();

        var conversationsQueryable = GetConversationsQueryable();
        int totalConversations = await conversationsQueryable.CountAsync();
        var conversationsByMonth = await CountByMonthIncludeUsAsync(conversationsQueryable);
        Conversations = new Metric(totalConversations, conversationsByMonth);

        ConversationStateCounts = (await _db.Conversations
#if !DEBUG
                .Where(s => !SeriousOrganizations.Contains(s.OrganizationId))
#endif
                .GroupBy(c => c.State)
                .Select(c => new {
                    c.Key,
                    Count = c.Count()
                })
                .ToListAsync())
            .Select(c => (c.Key, c.Count))
            .OrderBy(c => (int)c.Key)
            .ToList();

        var announcementsQuery = GetAnnouncementsQueryable();
        TotalAnnouncementsCount = await GetAnnouncementsQueryable().CountAsync();
        AnnouncementsByMonth = await CountByMonthIncludeUsAsync(announcementsQuery);

        var agentQueryable = _db.Members
            .Where(m => (m.Organization.PlanType == PlanType.Free && m.Organization.Trial!.Plan == PlanType.Business) || m.Organization.PlanType == PlanType.Business)
            .Where(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Agent));
        TotalAgentCount = await agentQueryable.CountAsync();
        AgentsCountByMonth = await CountByMonthIncludeUsAsync(agentQueryable);

        var taggedConversationsQueryable = _db.Conversations
            .Where(c => c.Tags.Any(t => !EF.Functions.Like(t.Tag.Name, "%:%")))
            .WhereNotUs();

        TotalTaggedConversationsCount = await taggedConversationsQueryable.CountAsync();
        TaggedConversationsByMonth = await CountByMonthIncludeUsAsync(taggedConversationsQueryable);

        var aiTaggedConversationsQueryable = _db.Conversations
            .Where(c => c.Tags.Any(t => EF.Functions.Like(t.Tag.Name, "%:%")))
            .WhereNotUs();

        TotalAITaggedConversationsCount = await aiTaggedConversationsQueryable.CountAsync();
        TaggedAIConversationsByMonth = await CountByMonthIncludeUsAsync(aiTaggedConversationsQueryable);

        var taggedCustomersQueryable = _db.Customers
            .Where(c => c.TagAssignments.Any())
            .WhereNotUs();

        TotalTaggedCustomersCount = await taggedCustomersQueryable.CountAsync();
        TaggedCustomersByMonth = await CountByMonthIncludeUsAsync(taggedCustomersQueryable);

        try
        {
            MonthlyRecurringRevenue = await StripeExtensions.CalculateMonthlyRecurringRevenue();
        }
        catch (Exception)
        {
            MonthlyRecurringRevenue = -1;
        }

        MaxIds["Members"] = await _db.Members.MaxAsync(m => m.Id);
        MaxIds["Users"] = await _db.Users.MaxAsync(m => m.Id);
        MaxIds["Organizations"] = await _db.Organizations.MaxAsync(m => m.Id);
        MaxIds["AuditEvents"] = await _db.AuditEvents.MaxAsync(m => m.Id);
        MaxIds["Rooms"] = await _db.Rooms.MaxAsync(m => (int?)m.Id) ?? 0;
        MaxIds["Conversations"] = await _db.Conversations.MaxAsync(m => (int?)m.Id) ?? 0;
        MaxIds["ConversationEvents"] = await _db.ConversationEvents.MaxAsync(m => (int?)m.Id) ?? 0;

        return Page();
    }

    IActionResult GetMetricsCsvResult(int take)
    {
        var metrics = Metrics.Take(take).OrderBy(m => m.Date);
        var rows = metrics.Select(m => $"{m.Date:O},{m.ActiveUserCount},{m.InteractionCount},{m.OrganizationCreatedCount},{m.UserCreatedCount},{m.SkillCreatedCount},{m.MonthlyRecurringRevenue}");
        var rowText = string.Join('\n', rows);
        return Content($"date,Active Users,Interactions,New Organizations,New Users,New Skills,MRR\n{rowText}");
    }

    IQueryable<Announcement> GetAnnouncementsQueryable() =>
        _db.Announcements.Include(s => s.Organization);

    IQueryable<Conversation> GetConversationsQueryable() =>
        _db.Conversations.Include(s => s.Organization);

    IQueryable<Member> GetMembersQueryable() =>
        _db.Members.Include(s => s.Organization);

    IQueryable<Skill> GetSkillsQueryable() =>
        _db.Skills
            .Include(s => s.Organization);

    IQueryable<Organization> GetOrganizationsQueryable() =>
        _db.Organizations.Where(o => o.PlanType != PlanType.None);

    IQueryable<AuditEventBase> GetHumanAuditEvents() =>
        _db.AuditEvents
            .Include(e => e.Organization)
            .Where(a => !(a is TriggerRunEvent));

    static async Task<IDictionary<DateTime, int>> CountSkillChildByMonthExcludeUsAsync<TEntity>(IQueryable<TEntity> queryable)
        where TEntity : class, ISkillChildEntity
    {
        return await queryable
            .Include(e => e.Skill)
            .Where(e => !SeriousOrganizations.Contains(e.Skill.OrganizationId))
            .GroupBy(o => new { o.Created.Year, o.Created.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(o => new { Date = new DateTime(o.Key.Year, o.Key.Month, 1), Count = o.Count() })
            .ToDictionaryAsync(o => o.Date, o => o.Count);
    }

    static async Task<IDictionary<DateTime, int>> CountByMonthExcludeUsAsync<TEntity>(IQueryable<TEntity> queryable)
        where TEntity : IOrganizationEntity
    {
        return await queryable
            .Where(s => !SeriousOrganizations.Contains(s.OrganizationId))
            .GroupBy(o => new { o.Created.Year, o.Created.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(o => new { Date = new DateTime(o.Key.Year, o.Key.Month, 1), Count = o.Count() })
            .ToDictionaryAsync(o => o.Date, o => o.Count);
    }

    static async Task<IDictionary<DateTime, int>> CountByMonthIncludeUsAsync<TEntity>(IQueryable<TEntity> queryable)
        where TEntity : IEntity
    {
        return await queryable
            .GroupBy(o => new { o.Created.Year, o.Created.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(o => new { Date = new DateTime(o.Key.Year, o.Key.Month, 1), Count = o.Count() })
            .ToDictionaryAsync(o => o.Date, o => o.Count);
    }

    static IDictionary<DateTime, int> CountMetricByMonthIncludeUs(
        IEnumerable<DailyMetricsRollup> metrics,
        Func<DailyMetricsRollup, int> metricSelector)
    {
        return metrics.GroupBy(o => new { o.Date.Year, o.Date.Month })
            .Select(o => new { Date = new DateTime(o.Key.Year, o.Key.Month, 1), Metric = o.Sum(metricSelector) })
            .ToDictionary(o => o.Date, o => o.Metric);
    }

    public async Task<Dictionary<Organization, int>> TopTenOrganizationsByInteractions(int days)
    {
        return await TopTenOrganizationsByEntity(GetHumanAuditEvents(), days);
    }

    public async Task<Dictionary<Organization, int>> TopTenOrganizationsByNewSkills(int days)
    {
        return await TopTenOrganizationsByEntity(GetSkillsQueryable(), days);
    }

    public async Task<Dictionary<Organization, int>> TopTenOrganizationsByAnnouncements(int days)
    {
        return await TopTenOrganizationsByEntity(GetAnnouncementsQueryable(), days);
    }

    public async Task<Dictionary<Organization, int>> TopTenOrganizationsByConversations(int days)
    {
        return await TopTenOrganizationsByEntity(GetConversationsQueryable(), days);
    }

    public async Task<Dictionary<Organization, int>> TopTenOrganizationsByEntity<TEntity>(IQueryable<TEntity> queryable, int days) where TEntity : IOrganizationEntity
    {
        return (await queryable
                .Where(e => e.Created > DateTime.UtcNow.AddDays(-1 * days))
                .GroupBy(e => e.OrganizationId)
                .Select(e => new { e.Key, Count = e.Count() })
                .OrderByDescending(stat => stat.Count)
                .Take(10)
                .ToListAsync())
            .ToDictionary(stat => _db.Organizations.Find(stat.Key)!, stat => stat.Count); // Ugh.
    }

    public string CalculateChange(double newNumber, double oldNumber)
    {
        var change = (newNumber - oldNumber) / oldNumber;
        var prefix = change > 0 ? "+" : "";
        return $"{prefix}{change:P1}";
    }

    public static DateTime Midnight => DateTime.Today.AddDays(1).Date;

    static string GetCohortSql(
        DateTime startDate,
        string interval,
        string userTable,
        string eventJoinColumn)
    {
        // TODO: Filter organizations to those who have a bot installed.

        var abbotFilter = userTable is "Users"
            ? @"AND U.""IsAbbot"" = 0"
            : null;

        var cohortNumberCalculation = interval is "month"
            ? "date_part('month', age(A.activity_date, U.cohort_date))"
            : "(A.activity_date - U.cohort_date)/7";

        // CREDIT: SQL adapted from https://www.holistics.io/blog/calculate-cohort-retention-analysis-with-sql/
        return $@"
-- Set of users created after our start date with at least one active event
with cohort_users as (
	SELECT DISTINCT
		date_trunc('{interval}', U.""Created"")::date as cohort_date,
		E.""{eventJoinColumn}"" as user_id
	FROM ""AuditEvents"" E
	INNER JOIN ""{userTable}"" U ON U.""Id"" = E.""{eventJoinColumn}""
	WHERE
            ""Discriminator"" NOT LIKE '%TriggerRunEvent'
	    AND U.""Created"" > '{startDate:yyyy-MM-dd}'
		{abbotFilter}
	ORDER BY 1, 2
),
-- Only look at activities for users who have ever been active.
cohort_activities as (
	SELECT
		""{eventJoinColumn}"" as user_id,
		date_trunc('{interval}', ""Created"")::date as activity_date
	FROM ""AuditEvents""
	WHERE ""{eventJoinColumn}"" IN (
		SELECT cohort_users.user_id
		FROM cohort_users
	)
),
-- (user_id, cohort_number): user X has activity in month number X
user_activities as (
    SELECT
        A.user_id,
        {cohortNumberCalculation} as cohort_number
    FROM cohort_activities A
    LEFT JOIN cohort_users U ON A.user_id = U.user_id
    GROUP BY 1, 2
),
-- (cohort_date, cohort_size)
cohort_size as (
    SELECT
        cohort_date,
        count(1) as cohort_count
    FROM cohort_users
    GROUP BY 1
    ORDER BY 1
),
-- (cohort_date, cohort_number, cohort_count)
B as (
    SELECT
        C.cohort_date,
        A.cohort_number,
        count(1) as cohort_count
    FROM user_activities A
    LEFT JOIN cohort_users C ON A.user_id = C.user_id
    GROUP BY 1, 2
)
-- our final value: (cohort_date, size, cohort_size, percentage)
SELECT
    B.cohort_date as Date,
    S.cohort_count as Size,
    B.cohort_number::int as Number,
    B.cohort_count::float / S.cohort_count as Percentage
FROM B
LEFT JOIN cohort_size S ON B.cohort_date = S.cohort_date
WHERE B.cohort_date IS NOT NULL
ORDER BY 1, 3
";
    }

    public async Task<CohortModel> GetMonthlyUserCohortsAsync()
    {
        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        string userCohortSql = GetCohortSql(
            currentMonth.AddMonths(-11),
            "month",
            "Users",
            "ActorId");
        try
        {
            var cohorts = await _db.Cohorts.FromSqlRaw(userCohortSql).ToListAsync();
            return new CohortModel(cohorts, "Month");
        }
        catch (Exception)
        {
            return CohortModel.Empty;
        }
    }

    public async Task<CohortModel> GetMonthlyOrganizationCohortsAsync()
    {
        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        string orgCohortSql = GetCohortSql(
            currentMonth.AddMonths(-11),
            "month",
            "Organizations",
            "OrganizationId");
        try
        {
            var cohorts = await _db.Cohorts.FromSqlRaw(orgCohortSql).ToListAsync();
            return new CohortModel(cohorts, "Month");
        }
        catch (Exception)
        {
            return CohortModel.Empty;
        }
    }
}

public record Metric(int TotalCount, IDictionary<DateTime, int> MonthlyCounts);
