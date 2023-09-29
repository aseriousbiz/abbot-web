using System;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <inheritdoc cref="IInt32Argument"/>
public class Int32Argument : Argument, IInt32Argument, IEquatable<Int32Argument>
{
    /// <summary>
    /// Constructor for <see cref="Int32Argument"/>.
    /// </summary>
    public Int32Argument()
    {
    }

    /// <summary>
    /// Constructor for <see cref="Int32Argument"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="originalText">The original text.</param>
    /// <param name="intValue">the parsed int value.</param>
    public Int32Argument(string value, string originalText, int intValue)
        : base(value, originalText)
    {
        Int32Value = intValue;
    }

    /// <inheritdoc cref="Int32Value"/>
    public int Int32Value { get; }

    public bool Equals(Int32Argument? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return base.Equals(other) && Int32Value == other.Int32Value;
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

        return Equals((Int32Argument)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Int32Value);
    }
}
