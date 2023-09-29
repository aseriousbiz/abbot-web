using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.AI;

/// <summary>
/// Represents a directive in a prompt response [!name:args]
/// </summary>
/// <remarks>
/// Until we transition our prompts to use the new directive syntax, we need to support both the old and new syntax.
/// </remarks>
/// <param name="Name">The name of the directive, such as "Signal"</param>
/// <param name="RawArguments">The exact content of the arguments, unparsed.</param>
/// <param name="Arguments">A parsed list of arguments.</param>
public partial record Directive(string Name, string RawArguments, IReadOnlyList<string> Arguments)
{
    const string LegacyDirectivePattern = $@"{{@(?<name>{Skill.NamePattern})(?:\((?<args>.*?)\))?}}";
    const string NewDirectivePattern = $@"\[!(?<name>{Skill.NamePattern})(?:\:(?<args>.*?))?\]";
    const string DirectivePattern = $@"(?:{LegacyDirectivePattern}|{NewDirectivePattern})";
    internal static readonly Regex DirectiveRegex = DirectiveParserRegex();

    /// <summary>
    /// Parses a directive into its component parts.
    /// </summary>
    /// <param name="directive">The directive in the form {@name(arg0,arg1,...,argN)}</param>
    /// <param name="result">The result of parsing the directive.</param>
    /// <returns>The parsed <see cref="Directive"/>.</returns>
    public static bool TryParse(string directive, [NotNullWhen(true)] out Directive? result)
    {
        var match = DirectiveRegex.Match(directive);
        if (!match.Success)
        {
            result = null;
            return false;
        }

        var name = match.Groups["name"].Value;
        if (match.Groups["args"].Success)
        {
            var args = match.Groups["args"].Value;
            var parsed = new Arguments(args).Select(a => a.Value).ToList();

            result = new Directive(name, args.Trim(), parsed);
            return true;
        }

        result = new Directive(name, string.Empty, Array.Empty<string>());
        return true;
    }

    public static Directive Parse(string directive)
    {
        return TryParse(directive, out var result)
            ? result
            : throw new ArgumentException("Invalid directive");
    }

    [GeneratedRegex(DirectivePattern, RegexOptions.Compiled)]
    private static partial Regex DirectiveParserRegex();

    public override string ToString() => $"[!{Name}:{RawArguments}]";
}
