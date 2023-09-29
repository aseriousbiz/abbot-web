using NodaTime;
using Serious.Abbot.Messages;

public class ArgumentExtensionsTests
{
    public class TheToLocalTimeMethod
    {
        [Theory]
        [InlineData("2pm", 14, 0, 0)]
        [InlineData("2:00pm", 14, 0, 0)]
        [InlineData("2:00:23pm", 14, 0, 23)]
        [InlineData("14:00", 14, 0, 0)]
        [InlineData("8:00", 8, 0, 0)]
        [InlineData("8:00am", 8, 0, 0)]
        [InlineData("1320", 13, 20, 0)]
        [InlineData("1320:23", 13, 20, 23)]
        public void ReturnsLocalTime(string value, int hours, int minutes, int seconds)
        {
            var argument = new Argument(value);

            var localTime = argument.ToLocalTime().GetValueOrDefault();

            Assert.Equal(hours, localTime.Hour);
            Assert.Equal(minutes, localTime.Minute);
            Assert.Equal(seconds, localTime.Second);
        }

        [Theory]
        [InlineData("13pm")]
        [InlineData("1:62pm")]
        [InlineData("13:00pm")]
        [InlineData("13:00am")]
        [InlineData("-1:00pm")]
        public void ReturnsNullForInvalidTime(string value)
        {
            var argument = new Argument(value);

            var localTime = argument.ToLocalTime();

            Assert.Null(localTime);
        }
    }

    public class TheToTimeZoneMethod
    {
        [Fact]
        public void ConvertsLocalTimeToAnotherTimeZone()
        {
            var now = Instant.FromDateTimeUtc(new DateTime(2023, 02, 01, 1, 2, 3, DateTimeKind.Utc));
            var localTime = new LocalTime(14, 13);
            var pacific = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];
            var eastern = DateTimeZoneProviders.Tzdb["America/New_York"];

            var converted = localTime.ToTimeZone(pacific, eastern, now).TimeOfDay;

            Assert.Equal(17, converted.Hour);
            Assert.Equal(13, converted.Minute);
        }

        [Fact]
        public void ConvertsLocalTimeToAnotherTimeZoneAtDSTChangeOver()
        {
            // March 12, 3 AM UTC is 7 PM Pacific, 9 PM Eastern
            var now = Instant.FromDateTimeUtc(new DateTime(2023, 03, 12, 3, 0, 0, DateTimeKind.Utc));
            // Because this local time is 2pm in LA, and it's currently 7pm in LA, we use 2pm tomorrow.
            var localTime = new LocalTime(14, 13);
            var pacific = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];
            var eastern = DateTimeZoneProviders.Tzdb["America/New_York"];

            var converted = localTime.ToTimeZone(pacific, eastern, now).TimeOfDay;

            Assert.Equal(17, converted.Hour);
            Assert.Equal(13, converted.Minute);
        }

        [Fact]
        public void ConvertsLocalTimeToAnotherTimeZoneDayAfterDSTChangeOver()
        {
            var now = Instant.FromDateTimeUtc(new DateTime(2023, 03, 13, 3, 0, 0, DateTimeKind.Utc));
            var localTime = new LocalTime(14, 13);
            var pacific = DateTimeZoneProviders.Tzdb["America/Los_Angeles"];
            var eastern = DateTimeZoneProviders.Tzdb["America/New_York"];

            var converted = localTime.ToTimeZone(pacific, eastern, now).TimeOfDay;

            Assert.Equal(17, converted.Hour);
            Assert.Equal(13, converted.Minute);
        }
    }

    public class TheToInt32Method
    {
        [Theory]
        [InlineData("#42", 42)]
        [InlineData("42", 42)]
        [InlineData("-42", -42)]
        [InlineData("cat", null)]
        [InlineData("", null)]
        public void ConvertsArgToInt(string arg, int? expected)
        {
            var argument = new Argument(arg);

            var result = argument.ToInt32();

            Assert.Equal(expected, result);
        }
    }
}
