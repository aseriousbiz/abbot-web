using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serious.Cryptography;

namespace Serious.Text;

/// <summary>
/// A message that has had its sensitive data replaced with sanitized placeholders.
/// </summary>
/// <param name="Message">The resulting message.</param>
/// <param name="Replacements">The replacements.</param>
public record SanitizedMessage(SecretString Message, IReadOnlyDictionary<string, SecretString> Replacements)
{
    /// <summary>
    /// Given a sanitized message and a dictionary of replacements, restores the original message.
    /// </summary>
    /// <param name="sanitizedPlainText">A message that may or may not contain sanitized values.</param>
    /// <param name="replacements">A mapping of sanitized values to the original values.</param>
    /// <returns></returns>
    public static string Restore(string sanitizedPlainText, IReadOnlyDictionary<string, SecretString> replacements)
    {
        return replacements
            .Aggregate(
                sanitizedPlainText,
                (current, kvp) => Regex.Replace(
                    current,
                    $@"\b{Regex.Escape(kvp.Key)}\b",
                    kvp.Value.Reveal(),
                    RegexOptions.IgnoreCase));
    }
}
