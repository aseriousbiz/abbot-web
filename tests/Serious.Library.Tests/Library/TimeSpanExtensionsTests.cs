using System;
using Serious;
using Xunit;

public class TimeSpanExtensionsTests
{
    public class TheFormatDurationMethod
    {
        [Theory]
        [InlineData(0, "0 seconds")]
        [InlineData(1, "1 second")]
        [InlineData(30, "30 seconds")]
        [InlineData(125, "2 minutes and 5 seconds")]
        [InlineData(7500, "2 hours and 5 minutes")]
        [InlineData(7515, "2 hours, 5 minutes, and 15 seconds")]
        public void FormatsDurations(int seconds, string expected)
        {
            var duration = TimeSpan.FromSeconds(seconds);

            var result = duration.FormatDuration();

            Assert.Equal(expected, result);
        }
    }
}
