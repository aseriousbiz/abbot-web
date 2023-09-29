using System.Collections.Generic;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Clients;

/// <summary>
/// Sends invitations to the specified user to log in to ab.bot.
/// </summary>
public class InvitationSender
{
    static readonly ILogger<InvitationSender> Log = ApplicationLoggerFactory.CreateLogger<InvitationSender>();

    readonly ISlackResolver _slackResolver;
    readonly ISlackApiClient _slackApiClient;
    readonly IOrganizationRepository _organizationRepository;
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;

    public InvitationSender(
        ISlackResolver slackResolver,
        ISlackApiClient slackApiClient,
        IOrganizationRepository organizationRepository,
        IAnalyticsClient analyticsClient,
        IClock clock)
    {
        _slackResolver = slackResolver;
        _slackApiClient = slackApiClient;
        _organizationRepository = organizationRepository;
        _analyticsClient = analyticsClient;
        _clock = clock;
    }

    /// <summary>
    /// Sends an invitation to the specified user to log in to ab.bot.
    /// </summary>
    /// <param name="recipientUserIds">The Slack User Ids of the recipients.</param>
    /// <param name="fromUserId">The Slack user Id of the sender.</param>
    /// <param name="organizationId">The Id of the organization.</param>
    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task SendInvitationAsync(IEnumerable<string> recipientUserIds, string fromUserId, int organizationId)
    {
        var organization = await _organizationRepository.GetAsync(organizationId).Require();
        using var orgScope = Log.BeginOrganizationScope(organization);
        var apiToken = organization.RequireAndRevealApiToken();

        foreach (var recipientId in recipientUserIds)
        {
            await SendInvitationAsync(recipientId, fromUserId, organization, apiToken);
        }
    }

    async Task SendInvitationAsync(string recipientUserId, string fromUserId, Organization organization, string apiToken)
    {
        var member = await _slackResolver.ResolveMemberAsync(recipientUserId, organization).Require();

        member.Active = true;
        member.InvitationDate = _clock.UtcNow;
        await _organizationRepository.SaveChangesAsync();

        var message = new MessageRequest(
            recipientUserId,
            $"<@{fromUserId}> invites you to manage conversations with Abbot. Visit https://ab.bot/ to log in and accept.")
        {
            Blocks = new List<ILayoutBlock>
            {
                new Section(new MrkdwnText($"You have been invited to join https://ab.bot/ by <@{fromUserId}>."))
            }
        };
        var result = await _slackApiClient.PostMessageWithRetryAsync(apiToken, message);

        if (!result.Ok)
        {
            Log.ErrorPostingSlackMessage(result.ToString());
        }
        _analyticsClient.Track(
            "Invitation Sent",
            AnalyticsFeature.Invitations,
            member,
            organization,
            new { success = result.Ok });
    }
}

static partial class InvitationSenderLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Error posting Slack invitation message {SlackError}")]
    public static partial void ErrorPostingSlackMessage(this ILogger<InvitationSender> logger, string? slackError);
}
