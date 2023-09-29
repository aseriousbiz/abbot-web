using System.Globalization;

public class WorkingHoursTests
{
    public class TheContainsMethod
    {
        [Theory]
        [InlineData("00:00", "00:00", "09:00", true)]
        [InlineData("09:00", "17:00", "09:00", true)]
        [InlineData("09:00", "17:00", "08:59", false)]
        [InlineData("09:00", "17:00", "17:00", false)]
        [InlineData("09:00", "17:00", "16:59", true)]
        [InlineData("17:00", "09:00", "20:00", true)]
        [InlineData("17:00", "09:00", "08:00", true)]
        [InlineData("17:00", "09:00", "09:00", false)]
        [InlineData("17:00", "09:00", "16:59", false)]
        public void ContainsReturnsTrueIfProvidedTimeIsWithinWorkingHours(
            string startTime,
            string endTime,
            string targetTime,
            bool expected)
        {
            var start = TimeOnly.ParseExact(startTime, "HH:mm");
            var end = TimeOnly.ParseExact(endTime, "HH:mm");
            var target = TimeOnly.ParseExact(targetTime, "HH:mm");
            var workingHours = new WorkingHours(start, end);
            Assert.Equal(expected, workingHours.Contains(target));
        }

        [Theory]
        [InlineData("09:00", "17:00", "17:00", "America/Los_Angeles", true)]
        [InlineData("09:00", "17:00", "15:00", "America/Los_Angeles", false)]
        [InlineData("09:00", "17:00", "15:00", "UTC", true)]
        [InlineData("09:00", "17:00", "01:00", "America/Los_Angeles", false)]
        public void ContainsReturnsTrueIfProvidedUtcTimeIsWithinWorkingHoursInTimezone(
            string startTime,
            string endTime,
            string targetTimeUtc,
            string tz,
            bool expected)
        {
            var start = TimeOnly.ParseExact(startTime, "HH:mm");
            var end = TimeOnly.ParseExact(endTime, "HH:mm");
            var target = DateTime.Parse("2020-01-01 " + targetTimeUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            var workingHours = new WorkingHours(start, end);

            var result = workingHours.Contains(target, tz);

            Assert.Equal(expected, result);
        }
    }

    public class TheDurationProperty
    {
        [Theory]
        [InlineData("00:00", "00:00", "1.00:00")] // AKA 24 hour coverage.
        [InlineData("00:00", "05:00", "05:00")]
        [InlineData("09:00", "17:00", "08:00")]
        [InlineData("08:45", "17:15", "08:30")]
        [InlineData("23:00", "02:00", "03:00")]
        [InlineData("23:01", "23:00", "23:59")]
        public void CalculatesDurationCorrectly(string startTime, string endTime, string expected)
        {
            var start = TimeOnly.ParseExact(startTime, "HH:mm");
            var end = TimeOnly.ParseExact(endTime, "HH:mm");
            var workingHours = new WorkingHours(start, end);

            var duration = workingHours.Duration;

            var expectedDuration = TimeSpan.Parse(expected);
            Assert.Equal(expectedDuration, duration);
        }
    }

    public class TheNextWorkingHoursStartDateUtcMethod
    {
        [Theory]
        [InlineData("09:00", "17:00", "2023-01-23T10:00:00Z", "UTC", "2023-01-24T09:00:00Z")]
        [InlineData("09:00", "17:00", "2023-01-23T10:00:00Z", "America/Los_Angeles", "2023-01-23T17:00:00Z")]
        [InlineData("09:00", "17:00", "2023-01-23T23:00:00Z", "America/Los_Angeles", "2023-01-24T17:00:00Z")]
        [InlineData("09:00", "17:00", "2023-01-24T01:00:00Z", "America/Los_Angeles", "2023-01-24T17:00:00Z")]
        [InlineData("09:00", "17:00", "2023-01-23T08:00:00Z", "UTC", "2023-01-23T09:00:00Z")]
        [InlineData("19:00", "06:00", "2023-01-23T07:00:00Z", "UTC", "2023-01-23T19:00:00Z")]
        [InlineData("19:00", "06:00", "2023-01-23T18:00:00Z", "UTC", "2023-01-23T19:00:00Z")]
        [InlineData("19:00", "06:00", "2023-01-23T20:00:00Z", "UTC", "2023-01-24T19:00:00Z")]
        public void ReturnsTheNextBeginningOfWorkingHours(
            string startTime,
            string endTime,
            string currentDate,
            string timezoneId,
            string expected)
        {
            var start = TimeOnly.ParseExact(startTime, "HH:mm");
            var end = TimeOnly.ParseExact(endTime, "HH:mm");
            var workingHours = new WorkingHours(start, end);
            var currentDateUtc = DateTime.Parse(
                 currentDate,
                 CultureInfo.InvariantCulture,
                 DateTimeStyles.AdjustToUniversal);
            var expectedDateUtc = DateTime.Parse(
                expected,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal);

            var result = workingHours.GetNextDateWithinWorkingHours(currentDateUtc, timezoneId);

            Assert.Equal(expectedDateUtc, result);
        }
    }
}
