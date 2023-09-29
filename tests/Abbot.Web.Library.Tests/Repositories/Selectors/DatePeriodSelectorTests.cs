using System;
using System.Globalization;
using System.Linq;
using NodaTime;
using NSubstitute;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.TestHelpers;
using Xunit;

public class DatePeriodSelectorTests
{
    // Apr 29, 2022 at 6:00 AM UTC
    static readonly DateTime NowUtc = new(2022, 04, 29, 6, 0, 0, DateTimeKind.Utc);

    static IEntity Create(int id, int day, int hour)
    {
        var entity = Substitute.For<IEntity>();
        entity.Id.Returns(id);
        entity.Created.Returns(new DateTime(NowUtc.Year, NowUtc.Month, day, hour, 0, 0, DateTimeKind.Utc));
        return entity;
    }

    public class TheEnumerateDaysMethod
    {
        [Theory]
        [InlineData("America/Los_Angeles", new[] { 22, 23, 24, 25, 26, 27, 28 })]
        [InlineData("Africa/Algiers", new[] { 23, 24, 25, 26, 27, 28, 29 })]
        [InlineData("UTC", new[] { 23, 24, 25, 26, 27, 28, 29 })]
        [InlineData("Asia/Tokyo", new[] { 23, 24, 25, 26, 27, 28, 29 })]
        public void ReturnsCorrectNumberOfDays(string timeZoneId, int[] expectedDays)
        {
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId)
                ?? throw new InvalidOperationException("We got big problems.");
            var clock = new TimeTravelClock();
            // * Fri, Apr 29 06:00 UTC
            // * Thu, Apr 28 23:00 America/Los_Angeles
            // * Fri, Apr 29 15:00 Asia/Tokyo
            // * Fri, Apr 29 07:00 Africa/Algiers
            clock.TravelTo(NowUtc);
            var selector = new DatePeriodSelector(7, clock, timeZone);

            var dates = selector.EnumerateDays().ToList();

            var days = dates.Select(d => d.Day).ToArray();
            Assert.Equal(expectedDays, days);
            Assert.All(dates, d => {
                Assert.Equal(2022, d.Year);
                Assert.Equal(4, d.Month);
            });
        }
    }

    public class TheGroupByLocalDateMethod
    {
        [Fact]
        public void ReturnsCorrectGroups()
        {
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles")
                           ?? throw new InvalidOperationException("We got big problems.");
            // NOW: Apr 20, 2022 00:00 UTC = Apr 19, 2022 5:00 PM PDT
            // Expected Range PDT: [Apr 13, 2022 12:00 AM PDT, Apr 20, 2022 00:00 PDT) <-- exclusive
            // Expected Range UTC: [Apr 13, 2022 07:00 AM UTC, Apr 20, 2022 07:00 UTC) <-- exclusive
            var nowUtc = Dates.ParseUtc("Apr 20, 2022 00:00");
            var clock = new TimeTravelClock();
            clock.TravelTo(nowUtc);
            IEntity CreateEntity((string, int) fixture)
            {
                var (date, id) = fixture;
                var entity = Substitute.For<IEntity>();
                entity.Id.Returns(id);
                entity.Created.Returns(Dates.ParseUtc($"2022, {date}"));
                return entity;
            }
            var fixtures = new[]
            {
                ("Apr 11 07:00", 10), // Out of range.
                ("Apr 12 00:00", 11), //  - Apr 12 PDT
                ("Apr 12 07:00", 12), //  - Apr 12 PDT

                ("Apr 13 07:00", 13), //  - Apr 13 PDT
                ("Apr 13 08:00", 14), //  - Apr 13 PDT
                ("Apr 14 06:00", 15), //  - Apr 13 PDT
                ("Apr 14 06:30", 16), //  - Apr 13 PDT

                ("Apr 14 07:30", 17), //  - Apr 14 PDT

                ("Apr 15 12:00", 18), //  - Apr 15 PDT
                //Apr 16
                ("Apr 17 09:00", 19), //  - Apr 17 PDT

                ("Apr 18 09:00", 20), //  - Apr 18 PDT
                ("Apr 18 09:00", 21), //  - Apr 18 PDT

                ("Apr 19 09:00", 22)  //  - Apr 19 PDT
            }.Select(CreateEntity);
            var selector = new DatePeriodSelector(7, clock, timeZone);

            var groups = selector
                .GroupByLocalDate(fixtures.AsQueryable().Apply(selector))
                .ToList();

            void AssertGroup(string expectedDate, int[] expectedIds, IGrouping<LocalDate, IEntity> group)
            {
                var actualIds = group.Select(e => e.Id).ToArray();
                var actualDate = group.Key.ToString("MMM dd", CultureInfo.InvariantCulture);
                Assert.Equal(expectedDate, actualDate);
                Assert.Equal(expectedIds, actualIds);
            }
            Assert.Collection(groups,
                g0 => AssertGroup("Apr 13", new[] { 13, 14, 15, 16 }, g0),
                g1 => AssertGroup("Apr 14", new[] { 17 }, g1),
                g2 => AssertGroup("Apr 15", new[] { 18 }, g2),
                g3 => AssertGroup("Apr 16", Array.Empty<int>(), g3),
                g4 => AssertGroup("Apr 17", new[] { 19 }, g4),
                g5 => AssertGroup("Apr 18", new[] { 20, 21 }, g5),
                g6 => AssertGroup("Apr 19", new[] { 22 }, g6));
        }
    }

    public class TheApplyMethod
    {
        [Theory]
        [InlineData("America/Los_Angeles", new[] { 2, 3, 4, 5 })]
        [InlineData("Asia/Tokyo", new[] { 3, 4, 5, 6 })]
        [InlineData("Africa/Algiers", new[] { 4, 5, 6, 7 })]
        [InlineData("UTC", new[] { 5, 6, 7, 8 })]
        public void FiltersEntitiesBetweenDatesAccountingForTimeZone(string timeZoneId, int[] expectedIds)
        {
            var queryable = new[]
            {
                new {Id = 1, Day = 21, Hour = 6},  // Nobody
                new {Id = 2, Day = 22, Hour = 7},  // America/Los_Angeles
                new {Id = 3, Day = 22, Hour = 15}, // America/Los_Angeles and Asia/Tokyo
                new {Id = 4, Day = 22, Hour = 23}, // America/Los_Angeles and Asia/Tokyo and Africa/Algiers
                new {Id = 5, Day = 23, Hour = 0},  // Errybody
                new {Id = 6, Day = 29, Hour = 7},  // Asia/Tokyo and Africa/Algiers and UTC only
                new {Id = 7, Day = 29, Hour = 15}, // Africa/Algiers and UTC only
                new {Id = 8, Day = 29, Hour = 23}, // UTC only
                new {Id = 9, Day = 30, Hour = 0},  // Nobody
            }
            .Select(t => Create(t.Id, t.Day, t.Hour))
            .AsQueryable();
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId)
                           ?? throw new InvalidOperationException("We got big problems.");
            var clock = new TimeTravelClock();
            // * America/Los_Angeles: Friday, Apr 22 07:00 UTC <= range < Friday, Apr 29 07:000 UTC
            // * Asia/Tokyo: Friday, Apr 22 15:00 UTC <= range < Friday, Apr 29 15:00 UTC
            // * Africa/Algiers: Friday, Apr 22 23:00 UTC <= range < Friday, Apr 29 23:00 UTC
            // * UTC: Saturday, Apr 23 00:00 UTC <= range < Saturday, Apr 30 00:00 UTC
            clock.TravelTo(NowUtc);
            var selector = new DatePeriodSelector(7, clock, timeZone);

            var result = queryable.Apply(selector).OrderBy(e => e.Id).Select(e => e.Id).ToArray();

            Assert.Equal(expectedIds, result);
        }
    }

    public class TheGroupByMethod
    {
        [Theory]
        [InlineData("America/Los_Angeles", new[] { "20:1", "22:2,3,4,5", "29:6,7,8,9" })]
        [InlineData("Asia/Tokyo", new[] { "21:1", "22:2", "23:3,4,5", "29:6", "30:7,8,9" })]
        [InlineData("Africa/Algiers", new[] { "21:1", "22:2,3", "23:4,5", "29:6,7", "30:8,9" })]
        [InlineData("UTC", new[] { "21:1", "22:2,3,4", "23:5", "29:6,7,8", "30:9" })]
        public void GroupsByDayTakingTimezoneIntoAccount(string timeZoneId, string[] expected)
        {
            var queryable = new[]
                {
                    new {Id = 1, Day = 21, Hour = 6},
                    new {Id = 2, Day = 22, Hour = 7},
                    new {Id = 3, Day = 22, Hour = 15},
                    new {Id = 4, Day = 22, Hour = 23},
                    new {Id = 5, Day = 23, Hour = 0},
                    new {Id = 6, Day = 29, Hour = 7},
                    new {Id = 7, Day = 29, Hour = 15},
                    new {Id = 8, Day = 29, Hour = 23},
                    new {Id = 9, Day = 30, Hour = 0},
                }
                .Select(t => Create(t.Id, t.Day, t.Hour))
                .AsQueryable();
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId)
                           ?? throw new InvalidOperationException("We got big problems.");
            var clock = new TimeTravelClock();
            // * America/Los_Angeles: Friday, Apr 22 07:00 UTC <= range < Friday, Apr 29 07:000 UTC
            // * Asia/Tokyo: Friday, Apr 22 15:00 UTC <= range < Friday, Apr 29 15:00 UTC
            // * Africa/Algiers: Friday, Apr 22 23:00 UTC <= range < Friday, Apr 29 23:00 UTC
            // * UTC: Saturday, Apr 23 00:00 UTC <= range < Saturday, Apr 30 00:00 UTC
            clock.TravelTo(NowUtc);
            var selector = new DatePeriodSelector(7, clock, timeZone);

            var result = selector.GroupByDay(queryable)
                .ToDictionary(g => g.Key.Day, g => g.Select(e => e.Id).ToArray());

            var groups = result.Select(e => $"{e.Key}:{string.Join(",", e.Value)}").ToArray();
            Assert.Equal(expected, groups);
        }
    }

    public class TheDatePeriodSelectorConstructor
    {
        [Fact]
        public void SetsDateRangesCorrectly()
        {
            var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles")
                           ?? throw new InvalidOperationException("We got big problems.");
            // NOW: Apr 20, 2022 00:00 UTC = Apr 19, 2022 5:00 PM PDT
            // Expected Range PDT: [Apr 13, 2022 12:00 AM PDT, Apr 20, 2022 00:00 PDT) <-- exclusive
            // Expected Range UTC: [Apr 13, 2022 07:00 AM UTC, Apr 20, 2022 07:00 UTC) <-- exclusive
            var nowUtc = Dates.ParseUtc("Apr 20, 2022 00:00");
            var clock = new TimeTravelClock();
            clock.TravelTo(nowUtc);
            var expectedStartDate = Dates.ParseUtc("Apr 13, 2022 07:00");
            var expectedEndDate = Dates.ParseUtc("Apr 20, 2022 07:00");

            var selector = new DatePeriodSelector(7, clock, timeZone);

            Assert.Equal(expectedStartDate, selector.StartDateTimeUtc);
            Assert.Equal(expectedEndDate, selector.EndDateTimeUtc);
        }
    }
}
