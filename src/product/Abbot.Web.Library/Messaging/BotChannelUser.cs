using System.Diagnostics.CodeAnalysis;
using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;
using Serious.Cryptography;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Base class for all channel users representing Abbot.
/// </summary>
public class BotChannelUser : ChannelUser
{
    /// <summary>
    /// Creates a <see cref="BotChannelUser" /> from an <see cref="Organization"/>.
    /// This is used when calling a skill from a source that has no <see cref="ConversationReference"/>
    /// such as the `abbot-cli` or Skill Editor console.
    /// </summary>
    /// <param name="organization">The organization this Abbot belongs to.</param>
    public static BotChannelUser GetBotUser(Organization organization)
    {
        return organization.PlatformType switch
        {
            PlatformType.Slack when organization.PlatformBotUserId is null =>
                new SlackPartialBotChannelUser(organization.PlatformId, organization.PlatformBotId, organization.BotName),
            PlatformType.Slack =>
                new SlackBotChannelUser(
                    platformId: organization.PlatformId,
                    botId: organization.PlatformBotId,
                    botUserId: organization.PlatformBotUserId,
                    displayName: organization.BotName,
                    botResponseAvatar: organization.BotResponseAvatar,
                    apiToken: organization.ApiToken,
                    scopes: organization.Scopes),
            _ => new BotChannelUser(organization.PlatformId, organization.PlatformBotUserId, organization.BotName)
        };
    }

    /// <summary>
    /// Constructs a <see cref="BotChannelUser"/>
    /// </summary>
    /// <param name="platformId">The platform-specific ID for the organization.</param>
    /// <param name="platformBotId">
    ///     The Bot's Id. This is used to compare incoming <see cref="ChannelAccount" /> mentions
    ///     to make sure they're not Abbot. For Slack, this is not the Bot's user id and usually
    ///     looks something like "B0136HA6VJ6"
    /// </param>
    /// <param name="displayName">The bot's name.</param>
    /// <param name="apiToken">The bot's API token.</param>
    /// <param name="scopes">The bot's scopes.</param>
    /// <param name="botResponseAvatar">Overrides the avatar shown for Abbot in Abbot's chat replies.</param>
    public BotChannelUser(string? platformId, string? platformBotId, string? displayName,
        SecretString? apiToken = null, string? scopes = null, string? botResponseAvatar = null)
        : base(platformId, platformBotId ?? "")
    {
        DisplayName = displayName ?? "abbot";
        BotResponseAvatar = botResponseAvatar;
        ApiToken = apiToken;
        Scopes = scopes;
    }

    /// <summary>
    /// The bot display name. This is the name users would mention to call Abbot in the chat platform.
    /// </summary>
    public string DisplayName { get; }

    /// <inheritdoc cref="Organization.BotResponseAvatar"/>
    /// <remarks>This is configured in the Admin settings page and stored in <see cref="Organization.BotResponseAvatar"/>.</remarks>
    public string? BotResponseAvatar { get; }

    /// <summary>
    /// Retrieve's the Bot's User Id.
    /// </summary>
    public virtual string UserId => Id;

    /// <summary>
    /// The API token for this bot.
    /// </summary>
    public SecretString? ApiToken { get; protected set; }

    /// <summary>
    /// The API scopes for this bot.
    /// </summary>
    public string? Scopes { get; protected set; }

    /// <summary>
    /// Retrieve the unprotected API token if <see cref="ApiToken"/> is not null and not empty.
    /// </summary>
    /// <param name="unprotectedApiToken">The unprotected API token.</param>
    /// <returns><c>true</c> if the token exists, else <c>false</c></returns>
    public bool TryGetUnprotectedApiToken([NotNullWhen(true)] out string? unprotectedApiToken)
    {
        if (ApiToken is { Empty: false } apiToken)
        {
            unprotectedApiToken = apiToken.Reveal();
            return true;
        }

        unprotectedApiToken = null;
        return false;
    }
}
