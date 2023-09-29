using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Scripting.Utilities;

/// <summary>
/// Used to retrieve information about Slack users.
/// </summary>
public interface IUsersClient
{
    /// <summary>
    /// Retrieves a handle that can be used to send messages to a user given their platform-specific ID (for example, the User ID 'Unnnnnnn' in Slack).
    /// </summary>
    /// <remarks>
    /// This method does not confirm that the user exists.
    /// If the user does not exist, sending a message to it will fail silently.
    /// </remarks>
    /// <param name="id">The ID of the user to retrieve</param>
    /// <returns>An <see cref="IUserMessageTarget"/>, suitable for use in <see cref="MessageOptions.To"/>, referring to the user.</returns>
    IUserMessageTarget GetTarget(string id);

    /// <summary>
    /// Retrieves details about a user, including custom profile fields.
    /// </summary>
    /// <param name="id">The ID of the user to retrieve</param>
    Task<AbbotResponse<IUserDetails>> GetUserAsync(string id);
}
