using System;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Extensions;

public static class SlackApiExtensions
{
    public static async Task<MessageResponse> SendMessageAsync(
        this ISlackApiClient apiClient,
        Organization organization,
        string channel,
        string text,
        params ILayoutBlock[] blocks)
    {
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            throw new ArgumentException($"The organization {organization.PlatformId} must have an API token.");
        }

        var message = new MessageRequest(channel, text)
        {
            Blocks = blocks
        };
        return await apiClient.PostMessageWithRetryAsync(apiToken, message);
    }

    public static async Task<MessageResponse> SendDirectMessageAsync(
        this ISlackApiClient apiClient,
        Organization organization,
        User recipient,
        string text,
        params ILayoutBlock[] blocks)
        => await apiClient.SendMessageAsync(organization, recipient.PlatformUserId, text, blocks);

    /// <summary>
    /// Makes sure the <see cref="Organization.BotName"/> and <see cref="Organization.BotAvatar"/> properties are
    /// set. If not, uses the Slack API to retrieve those values and sets them. Does not save changes to the
    /// database.
    /// </summary>
    /// <param name="slackApiClient">Slack API Client.</param>
    /// <param name="organization">The organization.</param>
    public static async Task EnsureBotNameAndAvatar(this ISlackApiClient slackApiClient, Organization organization)
    {
        if (organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            if (organization is { PlatformBotId: { Length: > 0 } } and (
            { PlatformBotUserId: null } or { BotAppName: null } or { BotAppId: null }))
            {
                var response =
                    await slackApiClient.GetBotsInfoAsync(apiToken, organization.PlatformBotId);
                if (response is { Body: { UserId: { Length: > 0 } } })
                {
                    organization.PlatformBotUserId = response.Body.UserId;
                    organization.BotAppName = response.Body.Name;
                    organization.BotAppId = response.Body.AppId;
                }
            }

            // Every Bot App in Slack has an associated Bot User. So the Bot Name we care about
            // is the Bot User Name. Unfortunately we have to call the `users.info` API method
            // to get that name. It's not included in the `bots.info` API.
            if (organization is { PlatformBotUserId: { Length: > 0 } })
            {
                var botUserId = organization.PlatformBotUserId;
                var botUserResponse = await slackApiClient.GetUserInfo(apiToken, botUserId);
                if (botUserResponse is { Body: { Profile: { } } })
                {
                    var profile = botUserResponse.Body.Profile;
                    organization.BotName = botUserResponse.Body.Profile.RealNameNormalized;
                    organization.BotAvatar = profile.Image72 ?? profile.Image48 ?? profile.Image24;
                }
            }
        }
    }
}
