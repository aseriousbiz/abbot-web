using Humanizer;

namespace Serious.TestHelpers;

public static class Dates
{
    public static DateTime ParseUtc(string dateTimeString)
    {
        return DateTime.SpecifyKind(DateTime.Parse(dateTimeString), DateTimeKind.Utc);
    }

    public static void AssertAreEqualWithinThreshold(DateTime expected, DateTime actual, TimeSpan threshold)
    {
        var withinThreshold = expected.Add(threshold) > actual && expected.Subtract(threshold) < actual;
        if (!withinThreshold)
        {
            throw new Exception($"Actual {actual} is not within {threshold.Humanize()} of expected {expected}");
        }
    }
}
