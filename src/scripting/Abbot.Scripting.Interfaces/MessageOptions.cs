namespace Serious.Abbot.Scripting;

/// <summary>
/// Options to customize how a message is sent.
/// </summary>
public class MessageOptions
{
    /// <summary>
    /// Gets or sets the target to send the message to.
    /// If not specified, the default target depends on how the skill was triggered, see remarks for details.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default message target depends on how the skill was triggered.
    /// </para>
    /// <para>
    /// If the skill was invoked by a top-level message in a room, the default target is to send another top-level message to the room the message was posted in.
    /// </para>
    /// <para>
    /// If the skill was invoked by a thread reply, the default target is to send to the same thread.
    /// </para>
    /// <para>>
    /// If the skill was invoked by a trigger, the default target is to send a top-level message to the room the trigger is attached to.
    /// </para>
    /// </remarks>
    public IMessageTarget? To { get; set; }
}
