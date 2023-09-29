using System.Collections.Generic;
using NodaTime;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents a chat user "message target", which is essentially an <see cref="IChatUser"/> but only contains the ID
/// of the user, making it suitable for sending to with <see cref="MessageOptions.To"/>.
/// </summary>
public interface IUserMessageTarget : IMessageTarget
{
    /// <summary>
    /// The ID of the user on their chat platform.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets an <see cref="IMessageTarget"/>, suitable for using in <see cref="MessageOptions.To"/>, for sending a message on the specified thread within this user's DMs with Abbot.
    /// </summary>
    /// <param name="threadId">The ID of the thread, within this user's DMs with Abbot, to send on.</param>
    /// <returns>An <see cref="IMessageTarget"/> representing that thread.</returns>
    public IMessageTarget GetThread(string threadId) => new MessageTarget(new ChatAddress(Address.Type, Address.Id, threadId));
}

/// <summary>
/// A user on the chat platform.
/// </summary>
public interface IChatUser : IUserMessageTarget, IWorker
{
    /// <summary>
    /// The username for the user on the platform. Note that in some platforms this can be changed by the
    /// user at any time. If you're storing information related to a user, always use the <see cref="IUserMessageTarget.Id"/>
    /// property to identify a user.
    /// </summary>
    /// <remarks>
    /// For Slack, this is the username provided to us by Bot Service, which can be wrong if the user has since
    /// changed their username. We need to use the Slack API the first time we create the user to get their
    /// real username.
    /// </remarks>
    string UserName { get; }

    /// <summary>
    /// The display name for the user if known. Otherwise the username.
    /// </summary>
    /// <remarks>
    /// For Slack, this is `profile.real_name` if available, <see cref="UserName"/>.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// The user's email, if known.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// The location of the user, if anything is known about their location.
    /// </summary>
    ILocation? Location { get; }
}

/// <summary>
/// Provides detailed information about a user, as returned from the Users client.
/// </summary>
public interface IUserDetails : IChatUser
{
    /// <summary>
    /// Gets a dictionary of custom fields for the user.
    /// The key of the dictionary is the _ID_ of the custom field.
    /// </summary>
    IDictionary<string, UserProfileField> CustomFields { get; }
}

/// <summary>
/// Provides detailed information about a user profile field, as returned from the Users client.
/// </summary>
public record UserProfileField
{
    /// <summary>
    /// The platform-specific ID of the user profile field.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The value of the user profile field.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// The alternate text for the user profile field.
    /// You should generally prefer to use this value when displaying the profile field.
    /// </summary>
    public string? Alt { get; set; }
}
