using Microsoft.Bot.Schema;
using Serious.Cryptography;

namespace Serious.Abbot.Messaging;

/// <summary>
/// The <see cref="ChannelAccount"/> for the Abbot Bot User from an incoming Slack message has a slightly
/// different format from a regular user. This class represents that incoming user and handles those changes.
/// </summary>
/// <remarks>
/// As an example, the Id for a bot <see cref="ChannelAccount" /> looks something like:
/// B0136HA6VJ6:T013108BYLS:C0141FZEY56
/// </remarks>
public class SlackBotChannelUser : BotChannelUser
{
    /// <summary>
    /// Constructs a full <see cref="SlackBotChannelUser"/> for regular chat messages where we have the full
    /// data needed.
    /// </summary>
    /// <remarks>
    /// Some Slack events don't provide us the Bot User Id in which case we need to create a
    /// <see cref="SlackPartialBotChannelUser"/> instead.
    /// </remarks>
    /// <param name="platformId">The platform-specific ID for the organization.</param>
    /// <param name="botId">The Bot Id. Looks something like B0136HA6VJ6.</param>
    /// <param name="botUserId">The Bot User Id. Looks something like U013WCHH9NU.</param>
    /// <param name="displayName">The Bot display name. Should be abbot in production.</param>
    /// <param name="apiToken">The bot's API token.</param>
    /// <param name="scopes">The bot's scopes.</param>
    /// <param name="botResponseAvatar">Overrides the avatar shown for Abbot in Abbot's chat replies.</param>
    public SlackBotChannelUser(string platformId, string? botId, string? botUserId, string? displayName,
        SecretString? apiToken = null, string? scopes = null, string? botResponseAvatar = null)
        : base(
            platformId: platformId,
            platformBotId: botId,
            displayName: displayName,
            botResponseAvatar: botResponseAvatar,
            apiToken: apiToken,
            scopes: scopes)
    {
        UserId = botUserId ?? "";
    }

    /// <summary>
    /// This is the Slack Bot's User Id (not to be confused with Bot Id). This usually starts with "U". For
    /// example, "U013WCHH9NU" is Abbot-Dev.
    /// </summary>
    public override string UserId { get; }

    /// <summary>
    /// Renders the user as a mention.
    /// </summary>
    /// <returns>The user mention.</returns>
    public override string ToString()
    {
        return $"<@{UserId}>";
    }
}
