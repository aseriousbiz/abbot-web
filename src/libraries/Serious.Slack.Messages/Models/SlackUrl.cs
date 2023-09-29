using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Serious;

/// <summary>
/// Represents a parsed slack url.
/// </summary>
/// <param name="Url">The URL.</param>
/// <param name="Subdomain">The Slack Subdomain.</param>
public abstract record SlackUrl(Uri Url, string Subdomain)
{
    static readonly Regex SlackUrlParser = new Regex(
        @"https?://(?<subdomain>[^\.]+)\.slack\.com/(?<type>[^/]+)/(?<roomOrUser>[^/]+)(?:/p(?<messageTs>\d+)(?:\?.*thread_ts=(?<threadTs>\d+\.\d+).*)?)?");

    /// <summary>
    /// Tries to parse a url into a <see cref="SlackUrl"/>.
    /// </summary>
    /// <param name="url">The URL to parse.</param>
    /// <param name="slackUrl">The resulting slack URL.</param>
    public static bool TryParse(string url, [NotNullWhen(true)] out SlackUrl? slackUrl)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return TryParse(uri, out slackUrl);
        }

        slackUrl = null;
        return false;
    }

    /// <summary>
    /// Tries to parse a url into a <see cref="SlackUrl"/>.
    /// </summary>
    /// <param name="url">The URL to parse.</param>
    /// <param name="slackUrl">The resulting slack URL.</param>
    public static bool TryParse(Uri url, [NotNullWhen(true)] out SlackUrl? slackUrl)
    {
        string NormalizeMessageId(string input)
        {
            if (input.Contains('.', StringComparison.Ordinal))
            {
                return input;
            }
            return $"{input[0..10]}.{input[10..]}";
        }

        if (SlackUrlParser.Match(url.ToString()) is not { Success: true } match)
        {
            slackUrl = null;
            return false;
        }

        var subdomain = match.Groups["subdomain"].Value;
        var id = match.Groups["roomOrUser"].Value;
        var type = match.Groups["type"].Value;
        var threadTs = match.Groups["threadTs"].Success
            ? NormalizeMessageId(match.Groups["threadTs"].Value)
            : null;
        var messageTs = match.Groups["messageTs"].Success
            ? NormalizeMessageId(match.Groups["messageTs"].Value)
            : null;

        if (type == "archives")
        {
            if (messageTs is not null)
            {
                slackUrl = new SlackMessageUrl(url, subdomain, id, messageTs, threadTs);
                return true;
            }

            slackUrl = new SlackConversationUrl(url, subdomain, id);
            return true;
        }

        if (type == "team")
        {
            slackUrl = new SlackUserUrl(url, subdomain, id);
            return true;
        }

        slackUrl = null;
        return false;
    }
}

/// <summary>
/// A Slack User URL.
/// </summary>
/// <param name="Url"></param>
/// <param name="Subdomain"></param>
/// <param name="UserId"></param>
public record SlackUserUrl(Uri Url, string Subdomain, string UserId) : SlackUrl(Url, Subdomain);

/// <summary>
/// Base class for a Slack conversation URL.
/// </summary>
/// <param name="Url"></param>
/// <param name="Subdomain"></param>
/// <param name="ConversationId"></param>
public abstract record SlackConversationUrlBase(Uri Url, string Subdomain, string ConversationId) : SlackUrl(Url, Subdomain);

/// <summary>
/// A Slack conversation URL.
/// </summary>
/// <param name="Url"></param>
/// <param name="Subdomain"></param>
/// <param name="ConversationId"></param>
public record SlackConversationUrl(Uri Url, string Subdomain, string ConversationId) : SlackConversationUrlBase(Url, Subdomain, ConversationId);

/// <summary>
/// A slack message URL.
/// </summary>
/// <param name="Url"></param>
/// <param name="Subdomain"></param>
/// <param name="ConversationId">The Id of the Slack conversation such as a user, channel, dm, etc. Not to be confused with an Abbot Conversation.</param>
/// <param name="Timestamp"></param>
/// <param name="ThreadTimestamp"></param>
public record SlackMessageUrl(Uri Url, string Subdomain, string ConversationId, string Timestamp,
    string? ThreadTimestamp) : SlackConversationUrlBase(Url, Subdomain, ConversationId);
