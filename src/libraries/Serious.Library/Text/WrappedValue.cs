using System.Diagnostics;

namespace Serious.Text;

/// <summary>
/// A class used to make it easy to wrap a string with extra information as a string and then unwrap it later.
/// This is often used when appending information to a string that's passed to another system that we need to
/// unwrap later.
/// </summary>
/// <remarks>
/// This assumes the extra information does not have a pipe <c>|</c> in it. The Original Value may contain a pipe.
/// Later on, we can consider base64 encoding the extra information, but this type is often used where we need to
/// keep the value of the resulting string short.
/// </remarks>
/// <param name="ExtraInformation">The extra information to add. Must not contain the <c>|</c> character.</param>
/// <param name="OriginalValue">The original value to wrap.</param>
public record WrappedValue(string ExtraInformation, string? OriginalValue)
{
    /// <summary>
    /// Parses a wrapped value into its original value and extra information. If the <c>|</c> character is not found,
    /// assumes the whole string is the extra information.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>A <see cref="WrappedValue"/>.</returns>
    public static WrappedValue Parse(string value)
    {
        var parts = value.Split('|', 2); // At most 2 parts
        var (extra, original) = parts.Length switch
        {
            1 => (value, null),
            2 => (parts[0], parts[1]),
            _ => throw new UnreachableException(),
        };
        return new WrappedValue(extra, original);
    }

    /// <summary>
    /// Converts the wrapped value to a string that can be parsed back into a <see cref="WrappedValue"/>.
    /// </summary>
    /// <returns>A pipe delimited string with the extra information and original value.</returns>
    public override string ToString()
    {
        return OriginalValue is not null
            ? $"{ExtraInformation}|{OriginalValue}"
            : ExtraInformation;
    }

    /// <summary>
    /// Implicit conversion to <c>string</c>.
    /// </summary>
    /// <param name="wrappedValue">The <see cref="WrappedValue"/> to represent as a string.</param>
    /// <returns>A delimited string containing the wrapped value and extra info.</returns>
    public static implicit operator string(WrappedValue wrappedValue) => wrappedValue.ToString();
}
