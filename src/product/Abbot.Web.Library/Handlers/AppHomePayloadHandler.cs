using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Used to handle displaying the App Home view and handling the interactions with that view.
/// </summary>
public class AppHomePayloadHandler : IPayloadHandler<AppHomeOpenedEvent>, IHandler
{
    static readonly ILogger<AppHomePayloadHandler> Log = ApplicationLoggerFactory.CreateLogger<AppHomePayloadHandler>();

    readonly AppHomePageHandler _appHomePageHandler;
    readonly IUserRepository _userRepository;
    readonly ISlackApiClient _slackApiClient;

    public AppHomePayloadHandler(
        AppHomePageHandler appHomePageHandler,
        IUserRepository userRepository,
        ISlackApiClient slackApiClient)
    {
        _appHomePageHandler = appHomePageHandler;
        _userRepository = userRepository;
        _slackApiClient = slackApiClient;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<AppHomeOpenedEvent> platformEvent)
    {
        var organization = platformEvent.Organization;

        if (organization.PlatformType != PlatformType.Slack)
        {
            // This is a Slack only event.
            return;
        }

        var member = platformEvent.From;

        if (!organization.Enabled)
        {
            Log.OrganizationDisabled();
            await PublishAppHomeDisabledPageAsync(platformEvent.Bot, member);
            return;
        }

        if (!member.Welcomed)
        {
            var botUserId = organization.PlatformBotUserId;
            var botMention = botUserId is null ? $"@{organization.BotName ?? "abbot"}" : SlackFormatter.UserMentionSyntax(botUserId);

            var reply = $":wave: Welcome to {organization.BotAppName ?? "Abbot"}!";
            if (!member.IsGuest && member.User.Email is null)
            {
                reply += $" Please let me know your email by replying `{botMention} my email is {{email}}` in this DM channel. This way we can get in touch if we have any important questions or updates for you.";
            }

            reply += " Have a great day!";

            await platformEvent.Responder.SendActivityAsync(reply, new UserMessageTarget(member.User.PlatformUserId));
            member.Welcomed = true;
            await _userRepository.UpdateUserAsync();
        }

        await _appHomePageHandler.PublishAppHomePageAsync(platformEvent.Bot, platformEvent.Organization, platformEvent.From);
    }

    async Task PublishAppHomeDisabledPageAsync(BotChannelUser bot, Member from)
    {
        var userId = from.User.PlatformUserId;
        var appHomeView = new AppHomeView
        {
            Blocks = new List<ILayoutBlock>
            {
                new Section($"Sorry, I cannot do that. Your organization is disabled. Please contact {WebConstants.SupportEmail} for more information.")
            },
        };
        var request = new PublishAppHomeRequest(userId, appHomeView);

        if (bot.TryGetUnprotectedApiToken(out var apiToken))
        {
            var response = await _slackApiClient.PublishViewAsync(apiToken, request);
            if (!response.Ok)
            {
                Log.ErrorCallingSlackApi(response.ToString());
            }
        }
    }
}
