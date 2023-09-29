using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Additional message options to customize delivery.
/// </summary>
/// <remarks>
/// This is a serializable version of <see cref="MessageOptions"/> (that type being designed for easy usage by Skills).
/// </remarks>
public class ProactiveBotMessageOptions
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
    public ChatAddress? To { get; init; }

    public static ProactiveBotMessageOptions FromMessageOptions(MessageOptions? options)
    {
        if (options is { To.Address: var a })
        {
            return new ProactiveBotMessageOptions
            {
                To = a
            };
        }

        return new ProactiveBotMessageOptions();
    }
}
