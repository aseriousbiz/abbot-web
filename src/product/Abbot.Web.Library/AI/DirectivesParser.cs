using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Serious.Abbot.AI;

public static class DirectivesParser
{
    /// <summary>
    /// Parses all the directives found in the given text.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>A list of <see cref="Directive"/>s containing the identified directives.</returns>
    public static IEnumerable<Directive> Parse(string text)
    {
        return Directive.DirectiveRegex.Matches(text).Select(match => Directive.Parse(match.Value));
    }

    /// <summary>
    /// Strips all directives from the given text.
    /// </summary>
    /// <param name="text">The text to strip.</param>
    /// <returns>The text, with all directives replaced with the empty string.</returns>
    public static string Strip(string text)
    {
        return Directive.DirectiveRegex.Replace(text, "");
    }
}
