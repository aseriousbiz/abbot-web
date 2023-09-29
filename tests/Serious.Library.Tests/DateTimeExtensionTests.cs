using System;
using System.Globalization;
using NodaTime;
using Serious.TestHelpers;
using Xunit;

public class DateTimeExtensionTests
{
    public class TheToLocalDateTimeMethod
    {
        [Theory]
        [InlineData("2004-01-20T07:00:00", "America/Los_Angeles", "2004-01-19 23:00:00")]
        [InlineData("2004-01-20T08:00:00", "America/Los_Angeles", "2004-01-20 00:00:00")]
        [InlineData("2004-01-20T08:00:00", "UTC", "2004-01-20 00:00:00")]
        [InlineData("2004-01-20T07:00:00", "Japan/Tokyo", "2004-01-19 23:00:00")]
        public void ConvertsDateTimeToLocalDate(string utcDate, string tz, string expected)
        {
            var dateTime = Dates.ParseUtc(utcDate);
            var result = dateTime.ToLocalDateTimeInTimeZone("America/Los_Angeles");
            Assert.Equal(expected, result.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
    }

    public class TheToUtcDateTimeMethod
    {
        [Fact]
        public void ConvertsDateOnlyAndTimeOnlyToUtc()
        {
            var tz = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];
            var dateOnly = new DateOnly(2004, 1, 19);
            var timeOnly = new TimeOnly(23, 0, 0);

            var result = dateOnly.ToUtcDateTime(timeOnly, tz);

            Assert.Equal("2004-01-20 07:00:00", result.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
    }
}
