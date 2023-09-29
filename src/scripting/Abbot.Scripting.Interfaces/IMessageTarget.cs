namespace Serious.Abbot.Scripting;

/// <summary>
/// A target to which messages can be sent. This could be a User, Channel, Thread, etc.
/// </summary>
public interface IMessageTarget
{
    /// <summary>
    /// Gets the <see cref="ChatAddress"/> used to reference this conversation.
    /// </summary>
    ChatAddress Address { get; }
}
