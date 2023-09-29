using Serious.Abbot.Scripting;

namespace Serious.Abbot;

/// <summary>
/// Utilities for working with Slack IDs.
/// </summary>
public static class SlackIdUtility
{
    /// <summary>
    /// Returns the <see cref="ChatAddressType" /> based on the slack Id.
    /// </summary>
    /// <param name="id">The Slack channel Id or user Id.</param>
    /// <returns>The <see cref="ChatAddressType"/> that corresponds to the Id.</returns>
    public static ChatAddressType? GetChatAddressTypeFromSlackId(string? id)
    {
        if (id is not { Length: > 0 })
        {
            return null;
        }
        return id[0] switch
        {
            'C' or 'G' => ChatAddressType.Room,
            'D' or 'U' or 'W' => ChatAddressType.User,
            _ => null
        };
    }

    /// <summary>
    /// If the Id starts with <c>E</c>, this is an Enterprise Id.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static bool IsEnterpriseId(string id) => id.StartsWith('E');
}
