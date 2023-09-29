using System.Globalization;

namespace Serious;

public static class NumericExtensions
{
    /// <summary>
    /// Returns a "clamped" string representation of the given number.
    /// That is, a version of ToString() that is guaranteed to be no larger than <paramref name="max"/>.
    /// If the value IS larger than <paramref name="max"/>, then the string representation will be max, plus the provided <paramref name="overflowSuffix"/>.
    /// </summary>
    /// <param name="val">The value to format.</param>
    /// <param name="max">The maximum allowed value.</param>
    /// <param name="overflowSuffix">The suffix to apply if the value is higher than the max (defaults to '+').</param>
    /// <returns>A clamped string.</returns>
    public static string ToClampedString(this int val, int max, string overflowSuffix = "+")
    {
        return val <= max ? $"{val}" : $"{max}{overflowSuffix}";
    }

    /// <summary>
    /// Returns the given number as a US currency amount (aka $).
    /// </summary>
    /// <param name="amount">The amount in US dollars.</param>
    /// <returns>A string formatted as US currency.</returns>
    public static string ToDollar(this decimal amount)
    {
        return amount.ToString("C", CultureInfo.GetCultureInfo("en-US"));
    }
}
