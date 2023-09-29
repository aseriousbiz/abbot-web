using Microsoft.Bot.Schema;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Wraps the <see cref="ChannelAccount"/> for the Abbot Bot User from an incoming Slack Interaction message
/// which does not contain the bot user id.
/// </summary>
public class SlackPartialBotChannelUser : BotChannelUser
{
    /// <summary>
    /// Constructs a <see cref="SlackPartialBotChannelUser" /> from an installation message. Unfortunately
    /// Slack doesn't include the Bot User Id in these messages.
    /// </summary>
    /// <param name="platformId">The platform-specific organization ID</param>
    /// <param name="botId">The BotId (aka B01234)</param>
    /// <param name="botName">The Bot display name.</param>
    public SlackPartialBotChannelUser(string platformId, string? botId, string? botName)
        : base(platformId, botId, botName)
    {
    }

    /// <summary>
    /// Renders the bot as a mention.
    /// </summary>
    /// <returns>The bot mention.</returns>
    public override string ToString()
    {
        // Since we don't have the bot's user id yet, we can't use the proper syntax.
        return $"@{DisplayName}";
    }
}
