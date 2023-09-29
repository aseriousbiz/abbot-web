using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Azure.AI.TextAnalytics;
using Humanizer;
using Serious.Cryptography;
using Serious.Text;

namespace Serious.Abbot.Services;

/// <summary>
/// Service used to replace sensitive data with similar "shaped" placeholders (emails look like emails, phone numbers
/// look like phone numbers, etc.) when sending messages to third party services. This can be used to restore the
/// original data when receiving a response.
/// </summary>
public static partial class SensitiveDataSanitizer
{
    // Regex adapted from https://stackoverflow.com/a/201378/598
    static readonly Regex EmailRegex = EmailParsingRegex();

    static readonly HashSet<PiiEntityCategory> DefaultUnredactedCategories = new();

    /// <summary>
    /// Scans the text for emails and returns a <see cref="SensitiveValue"/> instance for each found email.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    public static IEnumerable<SensitiveValue> ScanEmails(string text) =>
        Scan(text, EmailRegex, PiiEntityCategory.Email);

    static IEnumerable<SensitiveValue> Scan(string text, Regex regex, PiiEntityCategory category)
    {
        return regex.Matches(text).Select(m => SensitiveValue.FromRegexMatch(m, category));
    }

    // Dictionary of replacements to used for sensitive values. The
    // Func takes in the current count of replacement for that category as well as original value.
    static readonly Dictionary<PiiEntityCategory, Func<int, string, string>> ValueSanitizers = new()
    {
        [PiiEntityCategory.Email] = (count, _) => $"email.{count}@protected.ab.bot",
        [PiiEntityCategory.PhoneNumber] = (count, _) => $"555-100-{count:0000}",
        [PiiEntityCategory.CreditCardNumber] = (count, _) => GenerateInvalidCreditCardNumber(count),
        [PiiEntityCategory.USSocialSecurityNumber] = (count, _) => $"999-00-{count:0000}",
        [PiiEntityCategory.URL] = (count, _) => $"https://ab.bot/url/replacement-{count}",
        ["PersonType"] = (_, personType) => personType.Titleize(),
    };

    public static SanitizedMessage Sanitize(
        SecretString protectedText,
        IEnumerable<SensitiveValue> sensitiveValues,
        ISet<PiiEntityCategory>? unredactedCategories = null,
        IReadOnlyDictionary<string, SecretString>? existingReplacements = null)
        => Sanitize(protectedText.Reveal(), sensitiveValues, unredactedCategories, existingReplacements);

    /// <summary>
    /// Given some text and a set of sensitive values, replaces the sensitive values with a sanitized version and
    /// returns the sanitized message and a set of replacements that can be used to restore the original message.
    /// </summary>
    /// <param name="text">The text to sanitize.</param>
    /// <param name="sensitiveValues">The set of sensitive values.</param>
    /// <param name="unredactedCategories">The set of categories we do not want to redact.</param>
    /// <param name="existingReplacements">The existing set of replacements that seeds the replacements for the <see cref="SanitizedMessage"/> returned by this method.</param>
    /// <returns>A <see cref="SanitizedMessage"/> with the sanitized text and replacement strings to restore sensitive values.</returns>
    public static SanitizedMessage Sanitize(
        string text,
        IEnumerable<SensitiveValue> sensitiveValues,
        ISet<PiiEntityCategory>? unredactedCategories = null,
        IReadOnlyDictionary<string, SecretString>? existingReplacements = null)
    {
        unredactedCategories ??= DefaultUnredactedCategories;

#pragma warning disable CA1851
        if (text.Length is 0 || !sensitiveValues.Any())
        {
            return new SanitizedMessage(text, existingReplacements ?? new Dictionary<string, SecretString>());
        }

        Dictionary<string, SecretString> sanitizedToOriginal =
            existingReplacements?.ToDictionary(c => c.Key, c => c.Value)
            ?? new();

        Dictionary<string, string> originalToSanitized = sanitizedToOriginal
            .DistinctBy(v =>
                v.Value) // In theory we could lose values if the incoming replacements dictionary is badly formed, but that shouldn't happen so we don't care here.
            .ToDictionary(c => c.Value.Reveal(), c => c.Key);

        StringBuilder sanitizedText = new();

        var index = 0;
        // Loop through each match and replace the sensitive info with a replacement string.
        foreach (var value in sensitiveValues.OrderBy(v => v.Offset))
#pragma warning restore CA1851
        {
            if (unredactedCategories.Contains(value.Category))
            {
                continue;
            }

            if (!originalToSanitized.TryGetValue(value.Text, out var sanitizedValue))
            {
                var sanitizer = ValueSanitizers.TryGetValue(value.Category, out var func)
                    ? func
                    : (count, _) => GenericValueSanitizer(count, value.Category);

                sanitizedValue = sanitizer(sanitizedToOriginal.Count + 1, value.Text);
                sanitizedToOriginal[sanitizedValue] = new SecretString(value.Text);
                originalToSanitized[value.Text] = sanitizedValue;
            }

            // Append everything up until this match and the protected info.
            var toAppendLength = value.Offset - index;
            if (toAppendLength > 0)
            {
                sanitizedText.Append(text.AsSpan(index, toAppendLength));
            }

            sanitizedText.Append(sanitizedValue);
            // Update our current position.
            index = value.Offset + value.Length;
        }

        if (index < text.Length)
        {
            sanitizedText.Append(text[index..]);
        }

        return new SanitizedMessage(sanitizedText.ToString(), sanitizedToOriginal);
    }

    static string GenericValueSanitizer(int count, PiiEntityCategory category)
    {
        return $"{category}-{count}";
    }

    static string GenerateInvalidCreditCardNumber(int count)
    {
        if (count >= 10)
        {
            // Use the old logic.
            return $"0000 0000 0000 {count:0000}";
        }

        // CC numbers with all the same digits are known to be invalid.
        var digit = $"{count}"[0];
        var fourDigits = $"{new string(digit, 4)}";
        return $"{fourDigits} {fourDigits} {fourDigits} {fourDigits}";
    }

    [GeneratedRegex("\\b(?:[a-z]+[a-z.+]+@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\\[(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|[a-z0-9-]*[a-z0-9]:(?:[\\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x21-\\x5a\\x53-\\x7f]|\\\\[\\x01-\\x09\\x0b\\x0c\\x0e-\\x7f])+)\\]))\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex EmailParsingRegex();
}
