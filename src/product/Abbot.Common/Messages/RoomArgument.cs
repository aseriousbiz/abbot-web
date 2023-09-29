using System;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents an argument that is a room mention (for example `#room-name` in Slack).
/// </summary>
public class RoomArgument : Argument, IRoomArgument, IEquatable<RoomArgument>
{
    public RoomArgument()
    {
    }

    public RoomArgument(string value, string originalText, IRoom room)
        : base(value, originalText)
    {
        Room = room;
    }

    /// <summary>
    /// The mentioned room.
    /// </summary>

    public IRoom Room { get; init; } = null!;

    public bool Equals(RoomArgument? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return base.Equals(other) && Room.Equals(other.Room);
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

        return Equals((RoomArgument)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Room);
    }
}
