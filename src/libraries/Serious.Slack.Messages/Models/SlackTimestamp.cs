using System;

namespace Serious.Slack;

/// <summary>
/// Represents a Slack timestamp.
/// </summary>
public readonly record struct SlackTimestamp() : IComparable, IComparable<SlackTimestamp>
{
    const int UnixPartLength = 10;  //ex. 1657653155;
    const int TimestampLength = 17; //ex. 1657653155.174779;
    const int SuffixPartLength = 6; //ex. 174779;

    /// <summary>
    /// Constructs a <see cref="SlackTimestamp"/> from a <see cref="DateTime"/> and a suffix.
    /// </summary>
    /// <param name="utcDateTime">A UTC <see cref="DateTime"/>.</param>
    /// <param name="suffix">The suffix on Slack Id values used for uniqueness. Can represent milliseconds.</param>
    public SlackTimestamp(DateTime utcDateTime, string suffix = "000000") : this()
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be UTC.", nameof(utcDateTime));
        }

        UtcDateTime = utcDateTime;
        Suffix = suffix;
    }

    /// <summary>
    /// The UTC <see cref="DateTime"/> represented by this timestamp.
    /// </summary>
    public DateTime UtcDateTime { get; } = DateTime.UtcNow;

    /// <summary>
    /// The suffix on Slack Id values used for uniqueness. Can represent milliseconds.
    /// </summary>
    public string Suffix { get; } = "000000";

    /// <summary>
    /// Attempts to parse the Slack timestamp string into a <see cref="SlackTimestamp"/>.
    /// </summary>
    /// <param name="ts">The Slack timestamp string in the format <c>1657653155.174779</c></param>
    /// <param name="timestamp">The resulting timestamp.</param>
    /// <returns></returns>
    public static bool TryParse(string ts, out SlackTimestamp timestamp)
    {
        timestamp = default;

        if (ts.Length < UnixPartLength
            || ts.Length == TimestampLength && ts[10] != '.'
            || ts.Length > TimestampLength)
        {
            return false;
        }

        var unixSecondsPart = ts[..10];
        var suffixPart = ts.Length switch
        {
            TimestampLength => ts[11..],
            TimestampLength - 1 => ts[10..],
            _ => string.Empty
        };

        if (!IsValidUnixSeconds(unixSecondsPart, out var unixSeconds) || !IsValidSuffix(suffixPart))
        {
            return false;
        }

        var utcDateTime = DateTime.UnixEpoch.AddSeconds(unixSeconds);
        timestamp = new SlackTimestamp(utcDateTime, suffixPart);
        return true;
    }

    /// <summary>
    /// Attempts to parse the Slack timestamp string into a <see cref="SlackTimestamp"/>.
    /// </summary>
    /// <param name="ts">The Slack timestamp string in the format <c>1657653155.174779</c></param>
    /// <returns>The resulting <see cref="SlackTimestamp"/></returns>
    /// <exception cref="FormatException">Thrown if <paramref name="ts"/> cannot be parsed as a Slack timestamp.</exception>
    public static SlackTimestamp Parse(string ts) => TryParse(ts, out var timestamp)
        ? timestamp
        : throw new FormatException($"Invalid Slack timestamp {ts}.");

    static bool IsValidUnixSeconds(string unixSecondsPart, out long unixSeconds)
    {
        unixSeconds = 0;
        return unixSecondsPart.Length is UnixPartLength
               && long.TryParse(unixSecondsPart, out unixSeconds)
               && unixSeconds >= 0;
    }

    static bool IsValidSuffix(string suffix)
    {
        return suffix.Length is 0
            || suffix.Length is SuffixPartLength
            && int.TryParse(suffix, out var ms)
            && ms >= 0;
    }

    /// <summary>
    /// Returns the Slack timestamp string in the format <c>1657653155.174779</c>.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        var unixSeconds = (long)(UtcDateTime - DateTime.UnixEpoch).TotalSeconds;

        return Suffix is { Length: > 0 }
            ? $"{unixSeconds}.{Suffix}"
            : $"{unixSeconds}";
    }

    /// <summary>
    /// Compares this instance to another object and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
    /// </summary>
    /// <param name="obj">The object to compare to</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared.
    /// A value less than zero indicates this instance precedes <paramref name="obj"/> in the sort order.
    /// Zero indicates this instance occurs in the same position as <paramref name="obj"/> in the sort order.
    /// A value greater than zero indicates this instance follows <paramref name="obj"/> in the sort order.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the provided object is not a <see cref="SlackTimestamp"/>.
    /// </exception>
    public int CompareTo(object? obj) => obj is SlackTimestamp slackTs
        ? CompareTo(slackTs)
        : throw new ArgumentException("Object is not a SlackTimestamp.", nameof(obj));

    /// <summary>
    /// Compares this instance to another <see cref="SlackTimestamp"/> and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
    /// </summary>
    /// <param name="other">The <see cref="SlackTimestamp"/> to compare to</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared.
    /// A value less than zero indicates this instance precedes <paramref name="other"/> in the sort order.
    /// Zero indicates this instance occurs in the same position as <paramref name="other"/> in the sort order.
    /// A value greater than zero indicates this instance follows <paramref name="other"/> in the sort order.
    /// </returns>
    public int CompareTo(SlackTimestamp other) =>
        UtcDateTime.CompareTo(other.UtcDateTime) is var result && result != 0
            ? result
            : StringComparer.Ordinal.Compare(Suffix, other.Suffix);

    /// <summary>
    /// Compares two <see cref="SlackTimestamp"/> values and
    /// returns a boolean indicating if <paramref name="left"/> strictly precedes <paramref name="right"/> in the sort order.
    /// </summary>
    /// <param name="left">The left <see cref="SlackTimestamp"/> for the comparison</param>
    /// <param name="right">The right <see cref="SlackTimestamp"/> for the comparison</param>
    /// <returns>A boolean indicating if <paramref name="left"/> strictly precedes <paramref name="right"/> in the sort order.</returns>
    public static bool operator <(SlackTimestamp left, SlackTimestamp right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares two <see cref="SlackTimestamp"/> values and
    /// returns a boolean indicating if <paramref name="left"/> precedes, or is in the same position as, <paramref name="right"/> in the sort order.
    /// </summary>
    /// <param name="left">The left <see cref="SlackTimestamp"/> for the comparison</param>
    /// <param name="right">The right <see cref="SlackTimestamp"/> for the comparison</param>
    /// <returns>A boolean indicating if <paramref name="left"/> precedes, or is in the same position as,  <paramref name="right"/> in the sort order.</returns>
    public static bool operator <=(SlackTimestamp left, SlackTimestamp right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares two <see cref="SlackTimestamp"/> values and
    /// returns a boolean indicating if <paramref name="left"/> strictly follows <paramref name="right"/> in the sort order.
    /// </summary>
    /// <param name="left">The left <see cref="SlackTimestamp"/> for the comparison</param>
    /// <param name="right">The right <see cref="SlackTimestamp"/> for the comparison</param>
    /// <returns>A boolean indicating if <paramref name="left"/> strictly follows <paramref name="right"/> in the sort order.</returns>
    public static bool operator >(SlackTimestamp left, SlackTimestamp right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares two <see cref="SlackTimestamp"/> values and
    /// returns a boolean indicating if <paramref name="left"/> follows, or is in the same position as, <paramref name="right"/> in the sort order.
    /// </summary>
    /// <param name="left">The left <see cref="SlackTimestamp"/> for the comparison</param>
    /// <param name="right">The right <see cref="SlackTimestamp"/> for the comparison</param>
    /// <returns>A boolean indicating if <paramref name="left"/> follows, or is in the same position as,  <paramref name="right"/> in the sort order.</returns>
    public static bool operator >=(SlackTimestamp left, SlackTimestamp right) => left.CompareTo(right) >= 0;
}
