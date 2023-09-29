using System;
using System.Diagnostics.CodeAnalysis;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

public class Argument : IOriginalArgument, IEquatable<Argument>
{
    public Argument()
    {
    }

    public Argument(IOriginalArgument argument) : this(argument.Value, argument.OriginalText)
    {

    }

    public Argument(string value)
    {
        Value = value;
        OriginalText = value;
    }

    public Argument(string value, string originalText)
    {
        Value = value;
        OriginalText = originalText;
    }

    public string Value { get; init; } = string.Empty;

    public string OriginalText { get; init; } = string.Empty;

    public override string ToString()
    {
        return Value;
    }

    /// <summary>
    /// Attempts to parse the current token as a mention in the Abbot Normal Format (aka a Slack mention).
    /// A mention looks something like &lt;@PLATFORMUSERID&gt;
    /// </summary>
    /// <param name="token">The current argument token.</param>
    /// <param name="platformUserId">The parsed out platform user id.</param>
    /// <returns></returns>
    public static bool TryParseMention(string token, [NotNullWhen(true)] out string? platformUserId)
    {
        if (token.StartsWith("<@", StringComparison.Ordinal) && token.EndsWith('>'))
        {
            platformUserId = token[2..^1];
            return true;
        }

        platformUserId = null;
        return false;
    }

    /// <summary>
    /// Attempts to parse the current token as a room mention. A mention looks something like
    /// &lt;#room-id|room-name&gt; for Slack.
    /// </summary>
    /// <param name="token">The current argument token.</param>
    /// <param name="room">The parsed room.</param>
    public static bool TryParseRoom(string token,
        [NotNullWhen(true)] out IRoom? room)
    {
        if (token.StartsWith("<#", StringComparison.Ordinal) && token.EndsWith('>'))
        {
            var content = token[2..^1];
            var parts = content.Split('|');
            var (roomId, roomName) = parts.Length switch
            {
                1 => (content, string.Empty), // This shouldn't happen.
                2 => (parts[0], parts[1]),    // Probably Slack
                _ => (null, null)             // No idea what this is, abort!
            };
            if (roomId is not null && roomName is not null)
            {
                room = new PlatformRoom(roomId, roomName);
                return true;
            }
        }

        room = null;
        return false;
    }

    public bool Equals(Argument? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Value == other.Value && OriginalText == other.OriginalText;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((Argument)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, OriginalText);
    }
}
