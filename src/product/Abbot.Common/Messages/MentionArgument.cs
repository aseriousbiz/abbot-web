using System;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents an argument that is a user mention.
/// </summary>
public class MentionArgument : Argument, IMentionArgument, IEquatable<MentionArgument>
{
    public MentionArgument()
    {
    }

    public MentionArgument(string value, string originalText, IChatUser mentioned)
        : base(value, originalText)
    {
        Mentioned = mentioned;
    }

    /// <summary>
    /// The mentioned user.
    /// </summary>
    public IChatUser Mentioned { get; set; } = null!;

    public bool Equals(MentionArgument? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return base.Equals(other) && Mentioned.Equals(other.Mentioned);
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

        return Equals((MentionArgument)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Mentioned);
    }
}
