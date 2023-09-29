using System;
using System.Globalization;
using Serious.Slack;
using Xunit;

public class SlackTimestampTests
{
    public class TheConstructor
    {
        [Fact]
        public void ThrowsForNonUtcDates()
        {
            var dateTime = DateTime.Parse("2022-07-12T19:12:35.0000000Z", CultureInfo.InvariantCulture);
            Assert.Throws<ArgumentException>(() => new SlackTimestamp(dateTime, "000000"));
        }
    }

    public class TheTryParseMethod
    {
        [Theory]
        [InlineData("1657653155.174779", "2022-07-12T19:12:35.0000000Z", "174779")]
        [InlineData("1657653155174779", "2022-07-12T19:12:35.0000000Z", "174779")]
        [InlineData("1657653155", "2022-07-12T19:12:35.0000000Z", "")]
        public void CanParseValidTimestamps(string ts, string expectedDate, string expectedSuffix)
        {
            var result = SlackTimestamp.TryParse(ts, out var timestamp);

            Assert.Equal(expectedDate, timestamp.UtcDateTime.ToString("O"));
            Assert.Equal(expectedSuffix, timestamp.Suffix);
            Assert.True(result);
        }

        [Theory]
        [InlineData("-657653155.-74779")]
        [InlineData("1657653155-174779")]
        [InlineData("16576531557.174779")]
        [InlineData("165765315.5174779")]
        [InlineData("1657653155.0174779")]
        [InlineData("165765315")]
        [InlineData("165765315.74779")]
        [InlineData("thisis.bogus")]
        public void ReturnsFalseForInvalidTimestamps(string ts)
        {
            var result = SlackTimestamp.TryParse(ts, out var timestamp);

            Assert.False(result);
        }
    }

    public class TheToStringMethod
    {
        [Theory]
        [InlineData("2022-07-12T19:12:35.0000000Z", "174779", "1657653155.174779")]
        [InlineData("2022-07-12T19:12:35.0000000Z", "", "1657653155")]
        public void CanParseValidTimestamps(string utcDateString, string suffix, string expectedTs)
        {
            var utcDate = DateTime.Parse(utcDateString, CultureInfo.InvariantCulture);
            var ts = new SlackTimestamp(utcDate.ToUniversalTime(), suffix);

            var result = ts.ToString();

            Assert.Equal(expectedTs, result);
        }
    }
}
