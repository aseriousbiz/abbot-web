using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Logging;

namespace Serious.Abbot.Playbooks.Actions;

// Why isn't this static? Because there's a silly thing about static classes not being usable as generic type arguments.
// I'd rather just be able to use it in ILogger, so I'm leaving this non-static.
#pragma warning disable CA1052

public class ActionHelpers
{
    static readonly ILogger<ActionHelpers> Log =
        ApplicationLoggerFactory.CreateLogger<ActionHelpers>();

    /// <summary>
    /// Returns a value indicating whether the condition is true.
    /// </summary>
    public static bool EvaluateCondition(StepContext context)
    {
        var comparison = context.Expect<string>("comparison");

        var leftValue = context.Get<string>("left");

        if (Enum.TryParse<ExistenceComparisonType>(comparison, out var existenceComparison))
        {
            var existenceResult = (existenceComparison == ExistenceComparisonType.Exists && leftValue is { Length: > 0 })
                                  || (existenceComparison == ExistenceComparisonType.NotExists && leftValue is null or "");
            Log.ExistenceComparisonResult(leftValue, comparison, existenceResult);
            return existenceResult;
        }

        if (leftValue is null)
        {
            // A null left-hand side cannot satisfy any condition.
            Log.NoValueFound();
            return false;
        }

        var rightInput = context.Inputs.TryGetValue("right", out var r)
            ? r
            : null;

        if (r is JArray rightArray)
        {
            return EvaluateArrayCondition(leftValue, comparison, rightArray);
        }

        // In some cases, the left side of the expression is a JSON array of strings such as customer segments.
        // It could be an array of objects, but we don't support that yet.
        var values = (leftValue.StartsWith('[')
            ? JsonConvert.DeserializeObject<string[]>(leftValue)
            : null) ?? new[] { leftValue };

        var rightValue = r is JObject jObject
            ? jObject.Value<string>("value") ?? string.Empty // If we use react-select with is-multi to false, we get a single Option.
            : context.Expect<string>("right");

        var leftInput = context.Inputs.TryGetValue("left", out var l)
            ? l
            : null;

        var isMatch = Enum.TryParse<StringComparisonType>(comparison, out var stringComparison)
            ? values.Any(value => IsMatch(value, stringComparison, rightValue, false))
            : Enum.TryParse<NumberComparisonType>(comparison, out var numberComparisonType)
                ? values.Any(value => IsMatch(value, numberComparisonType, rightValue))
                : throw new InvalidOperationException($"Unexpected comparison type {comparison}");

        Log.ComparisonResult(leftInput, leftValue, rightInput, rightValue, comparison, isMatch);

        return isMatch;
    }

    static bool EvaluateArrayCondition(string left, string comparisonType, JArray right)
    {
        // For now, we have to assume that the left side is comma delimited string.
        var leftArray = left.Split(',');
        // And assume the right side is an array of options for which we'll grab the value.
        var rightValues = right
            .Select(token => token.Value<string>("value"))
            .WhereNotNull();

        if (Enum.TryParse<ArrayComparisonType>(comparisonType, out var arrayComparisonType))
        {
            return arrayComparisonType switch
            {
                // Left side contains all the values on the right side.
                ArrayComparisonType.All => rightValues.All(value => leftArray.Contains(value)),
                // Left side contains any of the values on the right side.
                ArrayComparisonType.Any => rightValues.Any(value => leftArray.Contains(value)),
                _ => throw new InvalidOperationException($"Unexpected comparison type {comparisonType}")
            };
        }

        return false;
    }

    static bool IsMatch(string text, StringComparisonType comparison, string pattern, bool caseSensitive)
    {
        var stringComparison = caseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return comparison switch
        {
            StringComparisonType.StartsWith => text.StartsWith(pattern, stringComparison),
            StringComparisonType.EndsWith => text.EndsWith(pattern, stringComparison),
            StringComparisonType.Contains => text.Contains(pattern, stringComparison),
            StringComparisonType.RegularExpression => IsRegexMatch(text, pattern, caseSensitive),
            StringComparisonType.ExactMatch => text.Equals(pattern, stringComparison),
            _ => false
        };
    }

    static bool IsMatch(string leftStr, NumberComparisonType comparison, string rightStr)
    {
        // If can't convert both to numbers, compare as strings
        var compared =
            decimal.TryParse(leftStr, CultureInfo.InvariantCulture, out var leftNum)
            && decimal.TryParse(rightStr, CultureInfo.InvariantCulture, out var rightNum)
            ? decimal.Compare(leftNum, rightNum)
            : string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);

        return comparison switch
        {
            NumberComparisonType.Equals => compared == 0,
            NumberComparisonType.GreaterThan => compared > 0,
            NumberComparisonType.GreaterThanOrEqualTo => compared >= 0,
            NumberComparisonType.LessThan => compared < 0,
            NumberComparisonType.LessThanOrEqualTo => compared <= 0,
            NumberComparisonType.NotEquals => compared != 0,
            _ => false
        };
    }

    static bool IsRegexMatch(string text, string pattern, bool caseSensitive)
    {
        var regexOptions = caseSensitive
            ? RegexOptions.None
            : RegexOptions.IgnoreCase;

        return Regex.IsMatch(text, pattern, regexOptions);
    }
}

public static partial class ActionHelpersLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "No value (\"left\" side of comparison) found to compare.")]
    public static partial void NoValueFound(this ILogger<ActionHelpers> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Compared {LeftInput} ({LeftValue}) {Comparison} {RightInput} ({RightValue}) with result: {Result}.")]
    public static partial void ComparisonResult(this ILogger<ActionHelpers> logger,
        object? leftInput,
        string leftValue,
        object? rightInput,
        string rightValue,
        string comparison,
        bool result);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Compared {LeftInput} ({Comparison}) with result: {Result}.")]
    public static partial void ExistenceComparisonResult(this ILogger<ActionHelpers> logger,
        object? leftInput,
        string comparison,
        bool result);
}
