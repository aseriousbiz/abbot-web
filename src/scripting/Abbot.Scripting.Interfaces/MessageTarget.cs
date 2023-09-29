namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents an arbitrary chat conversation.
/// </summary>
public class MessageTarget : IMessageTarget
{
    /// <summary>
    /// Gets the <see cref="ChatAddress"/> indicating how to send messages to this conversation.
    /// </summary>
    public ChatAddress Address { get; }

    /// <summary>
    /// Creates a new <see cref="MessageTarget"/> for the specified address.
    /// </summary>
    /// <param name="address">The <see cref="ChatAddress"/> to use when sending messages to this conversation.</param>
    public MessageTarget(ChatAddress address)
    {
        Address = address;
    }
}

/// <summary>
/// Represents a reply to a message in a thread.
/// </summary>
public class ReplyInThreadMessageTarget : MessageTarget
{
    /// <summary>
    /// Constructs a <see cref="ReplyInThreadMessageTarget"/>
    /// </summary>
    public ReplyInThreadMessageTarget(string platformRoomId, string threadId)
        : base(new ChatAddress(ChatAddressType.Room, platformRoomId, threadId))
    {
    }
}
