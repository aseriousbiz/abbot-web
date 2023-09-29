using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Models;

/// <summary>
/// Represents a chat room message target, which is essentially an <see cref="IRoom"/> but only contains the ID
/// of the room, making it suitable for sending to with <see cref="MessageOptions.To"/>.
/// </summary>
public class RoomMessageTarget : IRoomMessageTarget
{
    /// <summary>
    /// Constructs an instance of a room message target.
    /// </summary>
    /// <param name="id">The Id of the room.</param>
    public RoomMessageTarget(string id)
    {
        Id = id;
    }

    /// <summary>
    /// The platform specific identifier for the room. In some cases where the Id doesn't apply
    /// (such as the bot console), this is the name of the room.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the <see cref="ChatAddress"/> that can be used in the Reply API to send a message to this room.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public ChatAddress Address => new(ChatAddressType.Room, Id);
}
