using System;
using System.Globalization;
using System.Text.RegularExpressions;
using NodaTime;

namespace Serious.Abbot;

/// <summary>
/// Handles Slack formatting of mentions, room links, etc.
/// </summary>
public static partial class SlackFormatter
{
    /// <summary>
    /// Generates the platform-specific mention syntax for a user.
    /// </summary>
    /// <param name="userId">The platform-specific ID for the user being mentioned.</param>
    /// <returns>Mention syntax appropriate for using in a message.</returns>
    public static string UserMentionSyntax(string userId)
    {
        return $"<@{userId}>";
    }

    /// <summary>
    /// Given a Slack timestamp (ex. 1483037603.017503), returns the equivalent UTC <see cref="DateTime"/>.
    /// </summary>
    /// <param name="timestamp">The timestamp.</param>
    /// <returns></returns>
    public static DateTime GetDateFromSlackTimestamp(string timestamp)
    {
        var ts = timestamp[..timestamp.IndexOf('.', StringComparison.Ordinal)];
        var seconds = long.Parse(ts, CultureInfo.InvariantCulture);
        return Instant.FromUnixTimeSeconds(seconds).ToDateTimeUtc();
    }

    /// <summary>
    /// Generates a Slack date for the given timestamp and format.
    /// </summary>
    /// <param name="utcTime">The timestamp.</param>
    /// <param name="tokenString">
    /// Provides formatting for the timestamp, using plain text along with tokens
    /// like <c>{date_short_pretty}</c> and <c>{date}</c>. All tokens are documented at
    /// https://api.slack.com/reference/surfaces/formatting#date-formatting.
    /// </param>
    /// <param name="link">An optional link for the timestamp.</param>
    public static string FormatTime(
        DateTime utcTime,
        string tokenString = "{date_short_pretty} at {time}",
        string? link = null)
    {
        // https://api.slack.com/reference/surfaces/formatting#date-formatting
        var unixSeconds = (int)(utcTime - DateTime.UnixEpoch).TotalSeconds;

        // This tells Slack to render as "yesterday at 1:00am" or similar.
        // If the Slack client the user is using can't support these dates, we'll render an ISO UTC date
        // i.e. "2021-01-01T01:00:00Z". This should be _extremely_ rare.
        return $"<!date^{unixSeconds}^{tokenString}{(link is null ? "" : '^' + link)}|{utcTime:s}>";
    }

    /// <summary>
    /// Generates a platform-specific URL to launch the chat platform application and open the specified room.
    /// </summary>
    /// <param name="organizationDomain">The organization's domain.</param>
    /// <param name="roomId">The platform-specific ID of the room.</param>
    /// <returns>A URL that links directly to that room (if it exists).</returns>
    public static Uri RoomUrl(string? organizationDomain, string roomId)
    {
        if (organizationDomain is not { Length: > 0 })
        {
            throw new ArgumentException("The organization domain must be specified when platform type is 'Slack'", nameof(organizationDomain));
        }

        return new Uri($"https://{organizationDomain}/archives/{roomId}");
    }

    /// <summary>
    /// Generates a platform-specific URL to a specific message.
    /// </summary>
    /// <param name="organizationDomain">The organization's domain.</param>
    /// <param name="roomId">The platform-specific ID of the room in which the message was posted.</param>
    /// <param name="messageId">The platform-specific ID of the message.</param>
    /// <param name="threadId">The platform-specific ID of the thread containing the message.</param>
    /// <returns>A URL that links directly to that message (if it exists).</returns>
    public static Uri MessageUrl(
        string? organizationDomain,
        string roomId,
        string messageId,
        string? threadId = null)
    {
        if (organizationDomain is not { Length: > 0 })
        {
            throw new ArgumentException("The organization domain must be specified when platform type is 'Slack'", nameof(organizationDomain));
        }

        var url = $"https://{organizationDomain}/archives/{roomId}/p{messageId.Replace(".", "", StringComparison.Ordinal)}";

        if (threadId is { Length: > 0 })
        {
            url += $"?thread_ts={threadId}";
        }

        return new Uri(url);
    }

    /// <summary>
    /// Generates a platform-specific URL to a specific user.
    /// </summary>
    /// <param name="organizationDomain">The organization's domain.</param>
    /// <param name="userId">The platform-specific ID of the user.</param>
    /// <returns>A URL that links directly to that user (if they exist).</returns>
    public static Uri UserUrl(string? organizationDomain, string userId)
    {
        if (organizationDomain is not { Length: > 0 })
        {
            throw new ArgumentException("The organization domain must be specified when platform type is 'Slack'", nameof(organizationDomain));
        }

        return new Uri($"https://{organizationDomain}/team/{userId}");
    }

    /// <summary>
    /// Generates a platform-specific URL to a specific user group.
    /// </summary>
    /// <param name="organizationDomain">The organization's domain.</param>
    /// <param name="userGroupId">The platform-specific ID of the user group.</param>
    /// <returns>A URL that links directly to that user group (if it exists).</returns>
    public static Uri UserGroupUrl(string? organizationDomain, string userGroupId)
    {
        if (organizationDomain is not { Length: > 0 })
        {
            throw new ArgumentException("The organization domain must be specified when platform type is 'Slack'", nameof(organizationDomain));
        }

        return new Uri($"https://{organizationDomain}/threads/user_groups/{userGroupId}");
    }

    /// <summary>
    /// Converts a string into a valid channel name.
    /// </summary>
    /// <remarks>
    /// Conversation names can only contain lowercase letters, numbers, hyphens, and underscores, and must be 80
    /// characters or less. We will validate the submitted channel name and modify it to meet the above criteria.
    /// </remarks>
    /// <param name="value">The value to convert.</param>
    /// <returns>A string worthy of a Slack channel name.</returns>
    public static string ToSlackChannelName(this string value)
    {
        // We don't use Kebaberize because underscores are valid in Slack channel names and we want to
        // preserve them. In fact, we only want to replace spaces with dashes and assume everything else
        // in the way it should be.
        string result = ReplaceInvalidCharsWithDashRegex.Replace(value, "-").Trim('-').ToLowerInvariant();
        if (result is { Length: > 80 })
        {
            result = result[..80];
        }

        return result;
    }

    static readonly Regex ReplaceInvalidCharsWithDashRegex = StripInvalidSlackCharsRegex();

    // Note: - is valid, but we leave it out because we'll replace it with a dash anyways. This
    // ensures we don't create names with double dashes.
    [GeneratedRegex("[^a-zA-Z0-9_]+", RegexOptions.Compiled)]
    private static partial Regex StripInvalidSlackCharsRegex();
}
