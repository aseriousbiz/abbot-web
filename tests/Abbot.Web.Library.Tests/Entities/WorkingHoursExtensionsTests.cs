using System.Globalization;
using NodaTime;
using Serious.Abbot.Entities;

public class WorkingHoursExtensionsTests
{
    public class TheCollapseMethod
    {
        [Theory]
        [InlineData(new string[0], new string[0])]
        [InlineData(new[] { "09:00-17:00" }, new[] { "09:00-17:00" })]
        [InlineData(new[] { "22:00-04:00" }, new[] { "00:00-04:00", "22:00-00:00" })]
        [InlineData(new[] { "01:00-02:00", "02:00-03:00", "03:00-04:30" }, new[] { "01:00-04:30" })]
        [InlineData(new[] { "01:00-02:00", "02:30-03:00", "03:00-04:30" }, new[] { "01:00-02:00", "02:30-04:30" })]
        [InlineData(new[] { "18:00-02:00", "09:00-17:00", "16:30-18:00" }, new[] { "00:00-02:00", "09:00-00:00" })]
        [InlineData(new[] { "01:00-06:00", "05:00-23:00", "22:30-01:30" }, new[] { "00:00-00:00" })]
        public void CollapsesWorkingHoursIntoMinimalNonOverlappingSet(string[] sourceHours, string[] expectedHours)
        {
            var sources = sourceHours.Select(h => new WorkingHours(
                TimeOnly.ParseExact(h.Split('-')[0], "HH:mm"),
                TimeOnly.ParseExact(h.Split('-')[1], "HH:mm")));

            var expected = expectedHours.Select(h => new WorkingHours(
                TimeOnly.ParseExact(h.Split('-')[0], "HH:mm"),
                TimeOnly.ParseExact(h.Split('-')[1], "HH:mm")));

            Assert.Equal(expected.ToArray(), sources.Collapse().ToArray());
        }
    }

    public class TheChangeTimeZoneMethod
    {
        [Theory]
        [InlineData("09:00", "17:00", "America/Vancouver", "UTC", "16:00", "00:00")]
        [InlineData("16:00", "00:00", "UTC", "America/Vancouver", "09:00", "17:00")]
        [InlineData("17:00", "09:00", "America/Vancouver", "America/New_York", "20:00", "12:00")]
        public void ChangeTimeZoneWorks(string sourceStart, string sourceEnd, string sourceTz, string targetTz,
            string targetStart, string targetEnd)
        {
            var source = new WorkingHours(
                TimeOnly.ParseExact(sourceStart, "HH:mm"),
                TimeOnly.ParseExact(sourceEnd, "HH:mm"));

            var target = new WorkingHours(
                TimeOnly.ParseExact(targetStart, "HH:mm"),
                TimeOnly.ParseExact(targetEnd, "HH:mm"));

            var converted = source.ChangeTimeZone(
                new DateTime(2022, 04, 14, 0, 0, 0, DateTimeKind.Utc),
                DateTimeZoneProviders.Tzdb[sourceTz],
                DateTimeZoneProviders.Tzdb[targetTz]);

            Assert.Equal(target, converted);
        }
    }

    record WorkingStiff(WorkingHours? WorkingHours, DateTimeZone? TimeZone) : IWorker;

    public class TheCalculateCoverageMethod
    {
        [Fact]
        public void CalculatesCoverageInTargetTimeZone()
        {
            var nowUtc = new DateTime(2022, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            var stiffs = new[]
            {
                new WorkingStiff(new(new(13, 0), new(22, 30)),
                    DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York")),
                new WorkingStiff(new(new(8, 0), new(14, 0)),
                    DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Amsterdam")),
                new WorkingStiff(new(new(10, 0), new(17, 0)),
                    DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Vancouver")),
                new WorkingStiff(new(new(16, 30), new(19, 0)),
                    DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Vancouver")),
                new WorkingStiff(new(new(0, 0), new(20, 0)), null), // Ignored
            };

            var targetTz = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Vancouver").Require();

            var results = stiffs.CalculateCoverage(targetTz, WorkingHours.Default, nowUtc);

            Assert.Equal(new WorkingHours[]
                {
                    new(new(0, 0), new(5, 0)), new(new(10, 0), new(19, 30)), new(new(23, 0), new(0, 0)),
                },
                results.ToArray());
        }
    }

    public class TheCalculateResponseTimeWorkingHoursMethod
    {
        [Theory]
        [InlineData("2023-01-01 09:00", "2023-01-01 17:00", "08:00")] // Completely within working hours
        [InlineData("2023-01-01 09:00", "2023-01-01 16:59", "07:59")] // Completely within working hours
        [InlineData("2023-01-01 09:01", "2023-01-01 17:00", "07:59")] // Completely within working hours
        [InlineData("2023-01-01 09:01", "2023-01-01 16:59", "07:58")] // Completely within working hours.

        [InlineData("2023-01-01 06:00", "2023-01-01 19:00", "08:00")] // Started before and ended after working hours
        [InlineData("2023-01-01 00:00", "2023-01-02 00:00", "08:00")] // Full day special case.

        [InlineData("2023-01-01 06:00", "2023-01-01 17:00", "08:00")] // Started before working hours
        [InlineData("2023-01-01 08:59", "2023-01-01 16:59", "07:59")] // Started before working hours

        [InlineData("2023-01-01 09:01", "2023-01-01 23:00", "07:59")] // Ended after working hours
        [InlineData("2023-01-01 09:01", "2023-01-01 17:01", "07:59")] // Ended after working hours
        [InlineData("2023-01-01 10:00", "2023-01-02 00:00", "07:00")] // Ended after working hours

        [InlineData("2023-01-01 18:00",
            "2023-01-02 09:00",
            "00:00")] // Multi-day range, completely outside working hours
        [InlineData("2023-01-01 10:00", "2023-01-04 03:00", "23:00")] // Multi-day started inside, ended outside
        [InlineData("2023-01-01 18:00", "2023-01-04 10:30", "17:30")] // Multi-day started outside, ended inside
        [InlineData("2023-01-01 18:00", "2023-01-05 03:00", "1.00:00")] // Multi-day started and ended outside
        public void WithSingleWorkingHoursCalculatesResponseTimeWhenEndGreaterThanStart(
            string startDate,
            string endDate,
            string expected)
        {
            var workingHours = new WorkingHours(new(9, 0), new(17, 0));
            var start = DateTime.ParseExact(startDate, "yyyy-MM-dd HH:mm", null);
            var end = DateTime.ParseExact(endDate, "yyyy-MM-dd HH:mm", null);

            var elapsed = workingHours.CalculateResponseTimeWorkingHours(start, end);

            var expectedElapsed = TimeSpan.Parse(expected);
            Assert.Equal(expectedElapsed, elapsed);
        }

        [Theory]
        [InlineData("2023-01-01 00:00", "2023-01-01 05:00", "05:00")]
        [InlineData("2023-01-01 00:00", "2023-01-02 00:00", "05:00")]
        [InlineData("2023-01-01 00:00", "2023-01-01 01:00", "01:00")]
        [InlineData("2023-01-01 05:00", "2023-01-02 00:00", "00:00")]
        [InlineData("2023-01-01 00:00", "2023-01-02 01:00", "06:00")]
        [InlineData("2023-01-01 01:00", "2023-01-02 03:00", "07:00")]
        [InlineData("2023-01-01 01:00", "2023-01-04 03:00", "17:00")]
        public void WithSingleWorkingHoursHandlesWorkingHourStartingAtMidnight(
            string startDate,
            string endDate,
            string expected)
        {
            var workingHours = new WorkingHours(new(0, 0), new(5, 0));
            var start = DateTime.ParseExact(startDate, "yyyy-MM-dd HH:mm", null);
            var end = DateTime.ParseExact(endDate, "yyyy-MM-dd HH:mm", null);

            var elapsed = workingHours.CalculateResponseTimeWorkingHours(start, end);

            var expectedElapsed = TimeSpan.Parse(expected);
            Assert.Equal(expectedElapsed, elapsed);
        }

        [Theory]
        [InlineData("2023-01-01 00:00", "2023-01-01 05:00", "00:00")]
        [InlineData("2023-01-01 19:00", "2023-01-02 00:00", "04:00")]
        [InlineData("2023-01-01 19:00", "2023-01-02 09:00", "04:00")]
        [InlineData("2023-01-01 21:00", "2023-01-02 00:00", "03:00")]
        [InlineData("2023-01-01 21:00", "2023-01-01 23:59", "02:59")]
        [InlineData("2023-01-01 21:00", "2023-01-02 23:59", "06:59")]
        [InlineData("2023-01-01 21:00", "2023-01-03 05:00", "07:00")]
        public void WithSingleWorkingHoursHandlesWorkingHourEndingAtMidnight(
            string startDate,
            string endDate,
            string expected)
        {
            var workingHours = new WorkingHours(new(20, 0), new(0, 0));
            var start = DateTime.ParseExact(startDate, "yyyy-MM-dd HH:mm", null);
            var end = DateTime.ParseExact(endDate, "yyyy-MM-dd HH:mm", null);

            var elapsed = workingHours.CalculateResponseTimeWorkingHours(start, end);

            var expectedElapsed = TimeSpan.Parse(expected);
            Assert.Equal(expectedElapsed, elapsed);
        }

        [Theory]
        [InlineData("2023-01-01 05:00", "2023-01-01 21:59", "00:00")] // Completely outside of working hours.
        [InlineData("2023-01-01 00:01",
            "2023-01-01 03:01",
            "03:00")] // Completely inside the early part of working hours.
        [InlineData("2023-01-01 23:49",
            "2023-01-01 23:59",
            "00:10")] // Completely inside the later part of working hours.
        [InlineData("2023-01-01 03:00",
            "2023-01-01 05:00",
            "01:00")] // Started inside, but landed outside working hours.
        [InlineData("2023-01-01 21:00",
            "2023-01-01 23:59",
            "01:59")] // Started outside, but landed within working hours.

        [InlineData("2023-01-01 05:00",
            "2023-01-04 23:00",
            "19:00")] // Multi-day range, started inside, landed inside: First Day: 2, Last Day: 4 + 1 = 5, Duration: 6hrs * 2 = 12hrs,
        [InlineData("2023-01-01 23:01", "2023-01-04 03:00", "15:59")] // Multi-day range, started inside, landed inside
        [InlineData("2023-01-01 03:00", "2023-01-04 05:00", "19:00")] // Multi-day range, started outside, landed inside
        [InlineData("2023-01-01 05:00", "2023-01-04 03:00", "17:00")] // Multi-day range, started inside, landed outside
        public void WithSingleWorkingHoursCalculatesResponseTimeWhenEndLessThanThanStart(
            string startDate,
            string endDate,
            string expected)
        {
            var workingHours = new WorkingHours(new(22, 0), new(4, 0)); // 10pm - 4am (Duration: 6 hours)
            var start = DateTime.ParseExact(startDate, "yyyy-MM-dd HH:mm", null);
            var end = DateTime.ParseExact(endDate, "yyyy-MM-dd HH:mm", null);

            var elapsed = workingHours.CalculateResponseTimeWorkingHours(start, end);

            var expectedElapsed = TimeSpan.Parse(expected);
            Assert.Equal(expectedElapsed, elapsed);
        }

        [Theory]
        [InlineData("2022-04-19 01:00", "2022-04-19 02:00", "01:00")] // 6pm, 7pm
        [InlineData("2022-04-19 11:00", "2022-04-19 18:00", "02:00")] // 4am, 11am - crosses more than one working hours
        [InlineData("2022-04-19 11:00",
            "2022-04-22 18:00",
            "2.00:30")] // 4am, 11am - Start: 11:30 End: 06:00, Between: 31:00
        public void WithWorkersCalculatesResponseTimeForWorkingHoursOfWorkingStiffs(
            string startDateUtc,
            string responseDateUtc,
            string expected)
        {
            var startUtc = DateTime.ParseExact(startDateUtc, "yyyy-MM-dd HH:mm", null, DateTimeStyles.AssumeUniversal)
                .ToUniversalTime();

            var responseUtc = DateTime
                .ParseExact(responseDateUtc, "yyyy-MM-dd HH:mm", null, DateTimeStyles.AssumeUniversal)
                .ToUniversalTime();

            var stiffs = new[]
            {
                new WorkingStiff(new(new(13, 0), new(22, 30)),
                    DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/New_York")),
                new WorkingStiff(new(new(8, 0), new(14, 0)),
                    DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Amsterdam")),
                new WorkingStiff(new(new(10, 0), new(17, 0)),
                    DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Vancouver")),
                new WorkingStiff(new(new(16, 30), new(19, 0)),
                    DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Vancouver")),
                new WorkingStiff(new(new(0, 0), new(20, 0)), null), // Ignored
            };

            var result = stiffs.CalculateResponseTimeWorkingHours(
                WorkingHours.Default,
                startUtc,
                responseUtc);

            var expectedDuration = TimeSpan.Parse(expected);
            Assert.Equal(expectedDuration, result);
        }
    }
}
