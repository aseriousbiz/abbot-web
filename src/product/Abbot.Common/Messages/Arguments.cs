using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Models;

/// <summary>
/// Represents the arguments to the skill parsed into a collection of tokens.
/// </summary>
public class Arguments : List<IArgument>, IArguments
{
    public static readonly Arguments Empty = new();

    static readonly MissingArgument MissingArgument = new();

    static readonly Regex Tokenized = new(
        @"(?<value><@.+?>)           # Abbot Normalized Mention (aka Slack Mention) <@U12345>
                |(?<value><\#.+?>)         # Room Mention <#C01234|room-name> or <#12345>
                |'(?<value>.+?)'           # Single quoted value.
                |[\""](?<value>.+?)[\""]   # Double quoted value.
                |(?<value>\S+)           # Value without whitespace",
        RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

    /// <summary>
    /// Constructs a tokenized arguments collection in cases where we know there are no mentions.
    /// </summary>
    /// <param name="arguments">The original arguments.</param>
    public Arguments(string arguments) : this(ParseTokens(Normalize(arguments)))
    {
    }

    /// <summary>
    /// Constructs a tokenized arguments collection from the TokenizedArguments from the arguments passed in
    /// by the user. This is used to populate `MessageContext.Arguments`.
    /// </summary>
    /// <param name="arguments">The original arguments.</param>
    /// <param name="mentions">The mentioned users.</param>
    public Arguments(string arguments, IEnumerable<PlatformUser> mentions)
        : this(arguments, mentions.DistinctBy(u => u.Id).ToDictionary(u => u.Id))
    {
    }

    /// <summary>
    /// Constructs a tokenized arguments collection from the TokenizedArguments from the arguments passed in
    /// by the user. This is used to populate `MessageContext.Arguments`.
    /// </summary>
    /// <param name="arguments">The original arguments.</param>
    /// <param name="mentionsLookup">Dictionary of mentioned users with their username as keys.</param>
    Arguments(string arguments, IReadOnlyDictionary<string, PlatformUser> mentionsLookup)
        : this(ParseTokens(Normalize(arguments), mentionsLookup))
    {
    }

    /// <summary>
    /// Constructs a tokenized arguments collection from the TokenizedArguments sent to the .NET skill runner.
    /// </summary>
    /// <param name="arguments">The tokenized arguments.</param>
    /// <param name="rawArguments">The original raw arguments.</param>
    public Arguments(IEnumerable<IArgument> arguments, string rawArguments)
    {
        Value = rawArguments;
        AddRange(arguments);
    }

    /// <summary>
    /// Default constructor. Needed for serialization.
    /// </summary>
    public Arguments()
    {
        Value = string.Empty;
    }

    Arguments((string, IEnumerable<Argument>) normalizedAndTokenized)
    {
        var (value, args) = normalizedAndTokenized;
        Value = value;
        AddRange(args);
    }

    Arguments(IList<IArgument> arguments)
        : this(arguments, string.Join(' ', arguments.Cast<IOriginalArgument>().Select(og => og.OriginalText)))
    {
    }

    public void Deconstruct(out IArgument first, out IArgument second)
    {
        first = ElementAt(0);
        second = Rest(1);
    }

    public void Deconstruct(out IArgument first, out IArgument second, out IArgument third)
    {
        first = ElementAt(0);
        second = ElementAt(1);
        third = Rest(2);
    }

    public void Deconstruct(out IArgument first, out IArgument second, out IArgument third, out IArgument fourth)
    {
        first = ElementAt(0);
        second = ElementAt(1);
        third = ElementAt(2);
        fourth = Rest(3);
    }

    public void Deconstruct(out IArgument first, out IArgument second, out IArgument third, out IArgument fourth,
        out IArgument fifth)
    {
        first = ElementAt(0);
        second = ElementAt(1);
        third = ElementAt(2);
        fourth = ElementAt(3);
        fifth = Rest(4);
    }

    public void Deconstruct(out IArgument first, out IArgument second, out IArgument third, out IArgument fourth,
        out IArgument fifth, out IArgument sixth)
    {
        first = ElementAt(0);
        second = ElementAt(1);
        third = ElementAt(2);
        fourth = ElementAt(3);
        fifth = ElementAt(4);
        sixth = Rest(5);
    }

    /// <summary>
    /// Pops the first argument from the collection as the skill name, and returns the rest of the arguments as
    /// an <see cref="IArguments" /> collection.
    /// </summary>
    public (string skill, IArguments) Pop()
    {
        var skillArg = ElementAt(0);
        var args = new Arguments(Skip(1).ToList());
        return (skillArg.Value, args);
    }

    /// <summary>
    /// Retrieves the first argument from the collection that matches the condition, and returns that argument and
    /// the rest of the arguments as an <see cref="IArguments" /> collection. If the condition is not meant,
    /// <see cref="IMissingArgument" /> will be returned.
    /// </summary>
    public (IArgument argument, IArguments) FindAndRemove(Predicate<IArgument> condition)
    {
        var index = FindIndex(condition);
        if (index == -1)
        {
            return (new MissingArgument(), this);
        }

        var argument = ElementAt(index);
        var argsExceptFound = this.Where((_, i) => i != index).ToList();
        var args = new Arguments(argsExceptFound);
        return (argument, args);
    }

    /// <summary>
    /// Skips the specified number of arguments and returns the rest as an <see cref="IArguments"/> collection.
    /// </summary>
    /// <param name="count">The number of elements to skip.</param>
    public IArguments Skip(int count)
    {
        return count switch
        {
            < 0 => throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot skip a negative number of arguments."),
            0 => this,
            _ when count >= Count => Empty,
            _ => new Arguments(((IEnumerable<IArgument>)this).Skip(count).ToList())
        };
    }

    public IArguments this[Range range]
    {
        get {
            var (start, length) = range.GetOffsetAndLength(Count);
            return Slice(start, length);
        }
    }
    public IArguments Slice(int start, int length)
    {
        if (start < 0 || start >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0 || start + length > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var args = Skip(start).Take(length);
        return new Arguments(args.ToList());
    }

    IArgument ElementAt(int index)
    {
        return Count > index ? this[index] : MissingArgument;
    }

    public string Value { get; private set; }

    IArgument Rest(int index)
    {
        return index >= Count
            ? MissingArgument
            : index == Count - 1
                ? this[index]
                : new Arguments(this.Skip(index).ToList());
    }

    public override string ToString()
    {
        return Value;
    }


    static (string, IEnumerable<Argument>) ParseTokens(
        string arguments,
        IReadOnlyDictionary<string, PlatformUser>? mentions = null)
    {
        var tokenized = Tokenized
            .Matches(arguments)
            .Select(m => GetArgument(m.Groups["value"].Value, m.Value, mentions));

        return (arguments, tokenized);
    }

    static readonly Regex FixUrlRegex = new(
        @"(?<=\s|^|"")<(?<url>https?://.*?)(?:\|.*?)?>(?=""|\s|$)",
        RegexOptions.Compiled);

    static string Normalize(string value)
    {
        var normalized = value.Trim()
            .Replace("“", "\"", StringComparison.Ordinal)
            .Replace("”", "\"", StringComparison.Ordinal)
            .Replace("‘", "'", StringComparison.Ordinal)
            .Replace("’", "'", StringComparison.Ordinal);
        return FixUrlRegex.Replace(normalized, "${url}");
    }

    static Argument GetArgument(
        string value,
        string originalText,
        IReadOnlyDictionary<string, PlatformUser>? mentions)
    {
        if (mentions is not null
            && Argument.TryParseMention(value, out var platformUserId)
            && mentions.TryGetValue(platformUserId, out var user))
        {
            return new MentionArgument(value, originalText, user);
        }

        return Argument.TryParseRoom(value, out var room)
            ? new RoomArgument(value, originalText, room)
            : int.TryParse(value, out var intValue)
                ? new Int32Argument(value, originalText, intValue)
                : new Argument(value, originalText);
    }

    public void Prepend(string arguments)
    {
        InsertRange(0, new Arguments(arguments));
        Value = arguments + " " + Value;
    }
}
