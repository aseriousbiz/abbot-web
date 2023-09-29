using System;

namespace Serious.Filters;

/// <summary>
/// A filter.
/// </summary>
/// <param name="Field">The field this is a filters for.</param>
/// <param name="Value">The value for the filter.</param>
/// <param name="Include">If <c>true</c>, includes items that match the filter. Otherwise excludes items that match the filter.</param>
public record Filter(string? Field, string Value, string OriginalText, bool Include)
{
    public static Filter Create(string? field, string value) => Create(field, value, $"{field}:{FormatValue(value)}");

    public static Filter Create(string? field, string value, string originalText)
    {
        var include = true;
        field = field?.Trim().ToLowerInvariant();
        if (field is ['-', ..])
        {
            field = field[1..];
            include = false;
        }

        return new Filter(field, value, originalText, include);
    }

    public Filter(string Value) : this(null, Value, Value, Include: true)
    {
    }

    public string LowerCaseValue => Value.ToLowerInvariant();

    public override string ToString() => OriginalText;

    public static string FormatValue(string value) => value is { Length: > 0 } && value[0] == '"' && value[^1] == '"'
        ? value
        : value.Contains(' ', StringComparison.Ordinal)
            ? $"\"{value}\""
            : value;
}
