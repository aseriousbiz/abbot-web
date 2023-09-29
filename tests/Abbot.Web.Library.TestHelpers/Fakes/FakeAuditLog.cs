using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using Xunit;

namespace Serious.TestHelpers
{
    public class FakeAuditLog : AuditLog, IAuditLogReader
    {
        readonly IAuditLogReader _auditLogReader;
        readonly TimeTravelClock _clock;
        readonly CommonTestData? _testData;

        public FakeAuditLog(
            AbbotContext db,
            IAnalyticsClient analyticsClient,
            IClock clock,
            CommonTestData? testData = null)
            : base(db, analyticsClient, clock)
        {
            _auditLogReader = new AuditLogReader(db);
            _clock = clock.Require<TimeTravelClock>();
            _testData = testData;
        }

        public TimeSpan AdvanceByOnSave { get; set; }

        [Obsolete]
        protected override Task<TAuditEvent> SaveAuditEventAsync<TAuditEvent>(TAuditEvent auditEvent, User actor,
            Organization organization)
        {
            _clock.AdvanceBy(AdvanceByOnSave);
            return base.SaveAuditEventAsync(auditEvent, actor, organization);
        }

        /// <summary>
        /// Asserts facts about the most recent audit log event of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TEvent">The expected <see cref="AuditEventBase"/> type.</typeparam>
        /// <param name="description">The expected <see cref="AuditEventBase.Description"/>.</param>
        /// <param name="actor">The expected <see cref="AuditEventBase.Actor"/>.</param>
        /// <returns>The expected audit log event.</returns>
        public Task<TEvent> AssertMostRecent<TEvent>(
            string description,
            Member actor)
            where TEvent : AuditEventBase
            => AssertMostRecent<TEvent>(description, actor.User, actor.Organization);

        /// <summary>
        /// Asserts facts about the most recent audit log event of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TEvent">The expected <see cref="AuditEventBase"/> type.</typeparam>
        /// <param name="description">The expected <see cref="AuditEventBase.Description"/>.</param>
        /// <param name="actor">The expected <see cref="AuditEventBase.Actor"/>.</param>
        /// <param name="organization">The organization. If <c>null</c>, <see cref="CommonTestData.Organization"/> will be used.</param>
        /// <param name="errorMessage">The expected <see cref="AuditEventBase.ErrorMessage"/></param>
        /// <returns>The expected audit log event.</returns>
        public async Task<TEvent> AssertMostRecent<TEvent>(
            string description,
            User? actor = null,
            Organization? organization = null,
            string? errorMessage = null)
            where TEvent : AuditEventBase
        {
            var org = organization ?? _testData?.Organization;
            Assert.NotNull(org);

            var expectedActor = actor ?? _testData?.User;
            Assert.NotNull(expectedActor);

            var log = Assert.IsType<TEvent>(await GetMostRecentLogEntry(org));
            Assert.Equal(description, log.Description);
            Assert.Equal(expectedActor.Id, log.ActorId);
            return log;
        }

        /// <summary>
        /// Asserts there are no recent events of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TEvent">The not-expected <see cref="AuditEventBase"/> type.</typeparam>
        /// <param name="organization">The organization. If <c>null</c>, <see cref="CommonTestData.Organization"/> will be used.</param>
        public async Task AssertNoRecent<TEvent>(
            Organization? organization = null)
            where TEvent : AuditEventBase
        {
            var org = organization ?? _testData?.Organization;
            Assert.NotNull(org);

            var logs = await GetRecentActivityAsync(org);
            Assert.Empty(logs.OfType<TEvent>());
        }

        public async Task<T?> GetMostRecentLogEntryAs<T>(Organization organization) where T : AuditEventBase
        {
            var log = await _auditLogReader.GetRecentActivityAsync(organization, 1);
            return log.SingleOrDefault() switch
            {
                null => null,
                T t => t,
                var a => throw new Exception($"Expected {typeof(T).Name} but got {a.GetType().Name}")
            };
        }

        public async Task<AuditEventBase?> GetMostRecentLogEntry(Organization organization)
        {
            var log = await _auditLogReader.GetRecentActivityAsync(organization, 1, ActivityTypeFilter.All);
            return log.SingleOrDefault();
        }

        public Task<AuditEventBase?> GetAuditEntryAsync(Guid id)
        {
            return _auditLogReader.GetAuditEntryAsync(id);
        }

        public Task<IReadOnlyList<AuditEventBase>> GetRecentActivityAsync(Organization organization,
            int count = int.MaxValue, ActivityTypeFilter activityTypeFilter = ActivityTypeFilter.User)
        {
            return _auditLogReader.GetRecentActivityAsync(organization, count, activityTypeFilter);
        }

        public async Task<IReadOnlyList<AuditEvent>> GetRecentActivityAsync(Organization organization, AuditEventType type)
        {
            var allEvents = await _auditLogReader.GetRecentActivityAsync(organization, int.MaxValue);
            return allEvents.OfType<AuditEvent>().Where(e => e.Type == type).ToList();
        }

        public Task<IPaginatedList<AuditEventBase>> GetAuditEventsAsync(Organization organization, int pageNumber,
            int pageSize, StatusFilter statusFilter, ActivityTypeFilter activityTypeFilter, DateTime? minDate,
            DateTime? maxDate, bool includeStaff)
        {
            return _auditLogReader.GetAuditEventsAsync(organization,
                pageNumber,
                pageSize,
                statusFilter,
                activityTypeFilter,
                minDate,
                maxDate,
                includeStaff);
        }

        public Task<IPaginatedList<AuditEventBase>> GetAuditEventsForSkillAsync(Skill skill, int pageNumber,
            int pageSize, StatusFilter statusFilter,
            SkillEventFilter skillEventFilter, DateTime? minDate, DateTime? maxDate)
        {
            return _auditLogReader.GetAuditEventsForSkillAsync(skill,
                pageNumber,
                pageSize,
                statusFilter,
                skillEventFilter,
                minDate,
                maxDate);
        }

        public Task<IPaginatedList<AuditEventBase>> GetAuditEventsForSkillTriggerAsync(SkillTrigger trigger,
            int pageNumber, int pageSize, StatusFilter statusFilter,
            DateTime? minDate, DateTime? maxDate)
        {
            return _auditLogReader.GetAuditEventsForSkillTriggerAsync(trigger,
                pageNumber,
                pageSize,
                statusFilter,
                minDate,
                maxDate);
        }

        public Task<IPaginatedList<AuditEventBase>> GetAuditEventsForStaffAsync(
            int pageNumber,
            int pageSize,
            StatusFilter statusFilter,
            ActivityTypeFilter activityTypeFilter,
            DateTime? minDate,
            DateTime? maxDate)
        {
            return _auditLogReader.GetAuditEventsForStaffAsync(
                pageNumber,
                pageSize,
                statusFilter,
                activityTypeFilter,
                minDate,
                maxDate);
        }

        public Task<IReadOnlyList<AuditEventBase>> GetRelatedEventsAsync(AuditEventBase parentEvent) =>
            _auditLogReader.GetRelatedEventsAsync(parentEvent);
    }
}
