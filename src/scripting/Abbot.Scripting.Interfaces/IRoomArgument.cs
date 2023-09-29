namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents an argument that is a room mention (for example `#room-name` in Slack).
/// </summary>
public interface IRoomArgument : IArgument
{
    /// <summary>
    /// The mentioned room.
    /// </summary>
    IRoom Room { get; }
}
