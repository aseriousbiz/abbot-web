namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents a chat room "message target", which is essentially an <see cref="IRoom"/> but only contains the ID
/// of the room, making it suitable for sending to with <see cref="MessageOptions.To"/>.
/// </summary>
public interface IRoomMessageTarget : IMessageTarget
{
    /// <summary>
    /// The platform specific identifier for the room, if known.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets an <see cref="IMessageTarget"/>, suitable for using in <see cref="MessageOptions.To"/>, for sending a message on the specified thread within this room.
    /// </summary>
    /// <param name="threadId">The ID of the thread, within this room, to send on.</param>
    /// <returns>An <see cref="IMessageTarget"/> representing that thread.</returns>
    public IMessageTarget GetThread(string threadId) => new MessageTarget(new ChatAddress(Address.Type, Address.Id, threadId));
}

/// <summary>
/// Represents a chat room (channel in Slack parlance).
/// </summary>
public interface IRoom : IRoomMessageTarget
{
    /// <summary>
    /// The name of the room, if known.
    /// </summary>
    string? Name { get; }
}
