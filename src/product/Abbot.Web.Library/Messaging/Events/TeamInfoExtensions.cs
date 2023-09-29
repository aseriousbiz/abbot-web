using Serious.Slack;

namespace Serious.Abbot.Events;

public static class TeamInfoExtensions
{
    /// <summary>
    /// Retrieve the avatar from the <see cref="TeamInfo"/>.
    /// </summary>
    /// <param name="teamInfo">The <see cref="TeamInfo"/> returned by the Slack API.</param>
    public static string? GetAvatar(this TeamInfo teamInfo) => teamInfo.Icon.Image68;

    /// <summary>
    /// Retrieves the host name.
    /// </summary>
    /// <param name="teamInfo">The <see cref="TeamInfo"/> returned by the Slack API.</param>
    public static string GetHostName(this TeamInfo teamInfo) => $"{teamInfo.Domain}.slack.com";

    /// <summary>
    /// Retrieves the enterprise Id from the <see cref="TeamInfo"/>.
    /// </summary>
    /// <param name="teamInfo">The <see cref="TeamInfo"/> returned by the Slack API.</param>
    public static string GetEnterpriseId(this TeamInfo teamInfo) => teamInfo.EnterpriseId
        ?? (teamInfo.Id.StartsWith('E')
            ? teamInfo.Id
            : string.Empty);
}
