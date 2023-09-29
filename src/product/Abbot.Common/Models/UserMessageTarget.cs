using Serious.Abbot.Scripting;

namespace Serious.Abbot.Models;

/// <summary>
/// Represents a user message target, which is essentially an <see cref="IChatUser"/> but only contains the ID
/// of the user, making it suitable for sending to with <see cref="MessageOptions.To"/>.
/// </summary>
public class UserMessageTarget : IUserMessageTarget
{
    /// <summary>
    /// Constructs an instance of a user message target.
    /// </summary>
    /// <param name="id">The Id of the user.</param>
    public UserMessageTarget(string id)
    {
        Id = id;
    }

    /// <summary>
    /// The platform specific identifier for the user. In some cases where the Id doesn't apply
    /// (such as the bot console), this is the name of the user.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the <see cref="ChatAddress"/> that can be used in the Reply API to send a message to this user.
    /// </summary>
    public ChatAddress Address => new(ChatAddressType.User, Id);
}
