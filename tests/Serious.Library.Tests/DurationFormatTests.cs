using System;
using Serious;
using Xunit;

public class DurationFormatTests
{
    [Theory]
    [InlineData("2 week", 14, 0, 0, 0)]
    [InlineData("1 day", 1, 0, 0, 0)]
    [InlineData("1 day 4 hours 3m 12seconds", 1, 4, 3, 12)]
    [InlineData("1d22h60m3600s", 2, 0, 0, 0)]
    [InlineData("1.5 days 11.5 hours, 29.5; minutes, 30 seconds", 2, 0, 0, 0)]
    [InlineData("4d", 4, 0, 0, 0)]
    [InlineData("4day", 4, 0, 0, 0)]
    [InlineData("4days", 4, 0, 0, 0)]
    [InlineData("4h", 0, 4, 0, 0)]
    [InlineData("4hr", 0, 4, 0, 0)]
    [InlineData("4hrs", 0, 4, 0, 0)]
    [InlineData("4hour", 0, 4, 0, 0)]
    [InlineData("4hours", 0, 4, 0, 0)]
    [InlineData("4m", 0, 0, 4, 0)]
    [InlineData("4min", 0, 0, 4, 0)]
    [InlineData("4mins", 0, 0, 4, 0)]
    [InlineData("4minute", 0, 0, 4, 0)]
    [InlineData("4minutes", 0, 0, 4, 0)]
    [InlineData("4s", 0, 0, 0, 4)]
    [InlineData("4sec", 0, 0, 0, 4)]
    [InlineData("4secs", 0, 0, 0, 4)]
    [InlineData("4second", 0, 0, 0, 4)]
    [InlineData("4seconds", 0, 0, 0, 4)]
    [InlineData("4s;", 0, 0, 0, 4)]
    public void CanParseDurations(string input, double days, double hours, double minutes, double seconds)
    {
        var expectedTimeSpan = TimeSpan.FromDays(days) +
                               TimeSpan.FromHours(hours) +
                               TimeSpan.FromMinutes(minutes) +
                               TimeSpan.FromSeconds(seconds);
        Assert.True(DurationFormat.TryParse(input, out var actualTimeSpan));
        Assert.Equal(expectedTimeSpan, actualTimeSpan);
    }

    [Theory]
    [InlineData("-1 day")]
    [InlineData("1e4 day")]
    [InlineData("birth day")]
    [InlineData("1 day 2 hours 3 days 4 hrs")]
    public void IllegalDurations(string input)
    {
        Assert.False(DurationFormat.TryParse(input, out _));
    }
}
