using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Models;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Clients;

/// <summary>
/// Used to enqueue or schedule calls to the Slack API in the background. Note that methods enqueued or scheduled
/// to run in the background must be public.
/// </summary>
/// <remarks>
/// When enqueuing jobs with <see cref="IBackgroundJobClient"/>, the method enqueued must be public.
/// </remarks>
public class BackgroundSlackClient : IBackgroundSlackClient
{
    static readonly ILogger<BackgroundSlackClient> Log = ApplicationLoggerFactory.CreateLogger<BackgroundSlackClient>();

    readonly IOrganizationRepository _organizationRepository;
    readonly IBackgroundJobClient _backgroundJobClient;
    readonly IUserRepository _userRepository;
    readonly ISlackApiClient _slackApiClient;
    readonly IUrlGenerator _urlGenerator;
    readonly IClock _clock;

    /// <summary>
    /// Constructs a <see cref="BackgroundSlackClient"/>.
    /// </summary>
    /// <param name="organizationRepository">The <see cref="IOrganizationRepository"/>.</param>
    /// <param name="userRepository">The <see cref="IUserRepository"/>.</param>
    /// <param name="jobClient">The client used to enqueue background jobs.</param>
    /// <param name="slackClient">A Slack client.</param>
    /// <param name="urlGenerator">A url generator for all them tasty URLs.</param>
    /// <param name="clock">A clock.</param>
    public BackgroundSlackClient(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IBackgroundJobClient jobClient,
        ISlackApiClient slackClient,
        IUrlGenerator urlGenerator,
        IClock clock)
    {
        _organizationRepository = organizationRepository;
        _backgroundJobClient = jobClient;
        _userRepository = userRepository;
        _slackApiClient = slackClient;
        _urlGenerator = urlGenerator;
        _clock = clock;
    }

    /// <summary>
    /// Enqueue the update of an organization's set of OAuth scopes.
    /// </summary>
    /// <param name="organization">The organization to update.</param>
    public void EnqueueUpdateOrganizationScopes(Organization organization)
    {
        if (!organization.HasApiToken())
        {
            return;
        }
        _backgroundJobClient.Enqueue(() => UpdateOrganizationTeamInfoAsync(organization.Id));
    }

    /// <summary>
    /// Updates the organization info and users on a background task based on information retrieved from the Slack
    /// API.
    /// </summary>
    /// <param name="organization">The organization to update.</param>
    public void EnqueueUpdateOrganization(Organization organization)
    {
        if (!organization.HasApiToken())
        {
            return;
        }

        _backgroundJobClient.Enqueue(() => UpdateOrganizationTeamInfoAsync(organization.Id));
    }

    /// <summary>
    /// Sends a message in Slack to the user that installed Abbot to a new organization as a background task.
    /// </summary>
    /// <param name="organization">The organization the user belongs to.</param>
    /// <param name="installer">The <see cref="Member"/> who installed Abbot.</param>
    public void EnqueueMessageToInstaller(Organization organization, Member installer)
    {
        _backgroundJobClient.Schedule(() =>
            SendMessageToInstallerAsync(organization.Id, installer.User.PlatformUserId),
            TimeSpan.FromSeconds(0));
    }

    /// <summary>
    /// Sends a message to the user installing Abbot.
    /// </summary>
    /// <param name="organizationId">The Id of the organization.</param>
    /// <param name="slackUserId">The Id of the installing Slack user.</param>
    // ReSharper disable once MemberCanBePrivate.Global
    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task SendMessageToInstallerAsync(int organizationId, string slackUserId)
    {
        var organization = await _organizationRepository.GetAsync(organizationId);
        if (organization is null)
        {
            // We're done here.
            return;
        }

        // If api token is currently null, throw an exception and let the Hangfire retry logic try again later.
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            throw new InvalidOperationException(
                $"Cannot send message to {slackUserId} because organization {organizationId} does not yet have an API Token.");
        }

        var installer = await _userRepository.GetUserByPlatformUserId(slackUserId);
        var message = ":wave: Hi there! Thanks for installing Abbot!";
        if (organization.Trial is { Plan: PlanType.Business, Expiry: var expiry } && expiry > _clock.UtcNow)
        {
            message += $"\nI’ve gone ahead and activated your free trial. For the next {TrialPlan.TrialLengthDays} days you can use Abbot to track conversations created by guest and external members in your Slack workspace! :tada:\n";
        }
        if (installer?.Email is null)
        {
            message += "\nPlease let me know your email by replying `my email is {email}` in this DM channel. This way we can get in touch if we have any important questions or updates for you.\n";
        }

        var botMention = organization.PlatformBotUserId is { } botUserId
            ? SlackFormatter.UserMentionSyntax(botUserId)
            : "@abbot";
        message += $"\nHere’s what to do next\n:one: Invite Abbot to channels by posting `/invite {botMention}` in the"
            + " channel where you would like conversations tracked.\n:two: Configure conversation tracking in those "
            + "channels to ensure guest and external Slack member conversations are not missed.\n\nExtras:\n"
            + ":key: Add additional Administrators and members using the button below to ensure "
            + "continued access.\n"
            + ":envelope: Invite others to log into the Abbot website\n"
            + ":gear: Customize how Abbot fits in with your organization and responds to conversations.";

        Log.AboutToSendWelcomeMessage(slackUserId, organization.PlatformId, organization.Name);

        var messageRequest = AdminWelcomeHandler.GetAdminWelcomeMessageBlocks(
            message,
            slackUserId,
            _urlGenerator.InviteUsersPage(),
            _urlGenerator.OrganizationSettingsPage(),
            includeReminderButton: true);

        var response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest);
        if (!response.Ok)
        {
            Log.ErrorSendingDirectMessage(
                slackUserId,
                organization.Name,
                organization.PlatformId,
                organization.Id,
                response.ToString());
        }
    }

    public void EnqueueDirectMessages(Organization organization, IEnumerable<Member> members, string message)
    {
        if (organization.PlatformType != PlatformType.Slack)
        {
            return;
        }
        var slackUserIds = members.Select(m => m.User.PlatformUserId).ToList();

        _backgroundJobClient.Schedule(() => SendDirectMessagesAsync(
            organization,
            slackUserIds,
            message), TimeSpan.FromSeconds(1));
    }

    public async Task SendDirectMessagesAsync(Id<Organization> organizationId, IEnumerable<string> slackUserIds, string message)
    {
        var organization = await _organizationRepository.GetAsync(organizationId);
        if (organization is null)
        {
            // We're done here.
            return;
        }

        // If api token is currently null, throw an exception and let the Hangfire retry logic try again later.
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            throw new InvalidOperationException(
                $"Cannot send message to {string.Join(',', slackUserIds)} because organization {organizationId} does not yet have an API Token.");
        }

        foreach (var userId in slackUserIds)
        {
            var response = await _slackApiClient.PostMessageAsync(apiToken, userId, message);
            if (!response.Ok)
            {
                Log.ErrorSendingDirectMessage(
                    userId,
                    organization.Name,
                    organization.PlatformId,
                    organization.Id,
                    response.ToString());
            }
        }
    }

    /// <summary>
    /// Makes a call to the Slack API to get information about a team and updates the organization with that
    /// information including the set of OAuth scopes.
    /// </summary>
    /// <param name="organizationId">The id of the organization.</param>
    [Queue(HangfireQueueNames.Maintenance)]
    public async Task UpdateOrganizationTeamInfoAsync(int organizationId)
    {
        Log.AboutToUpdateOrganization(organizationId);

        var organization = await _organizationRepository.GetAsync(organizationId);
        if (organization is null)
        {
            Log.EntityNotFound(organizationId, typeof(Organization));
            return;
        }

        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            return;
        }

        var response = await _slackApiClient.GetTeamInfoWithOAuthScopesAsync(
            apiToken,
            organization.PlatformId);

        if (!response.Ok)
        {
            return;
        }

        if (response.Body.Scopes.Length > 0)
        {
            organization.Scopes = response.Body.Scopes;
        }
        var info = response.Body;

        await _organizationRepository.UpdateOrganizationAsync(organization, info);
    }

    public void EnqueueAdminWelcomeMessage(Organization organization, Member admin, Member actor)
    {
        if (organization.PlatformType != PlatformType.Slack)
        {
            return;
        }

        if (!admin.IsAdministrator())
        {
            throw new InvalidOperationException(
                $"Member {admin.Id} ({admin.User.PlatformUserId}) is not an administrator");
        }

        var newAdminUserId = admin.User.PlatformUserId;
        var actorUserId = actor.User.PlatformUserId;

        _backgroundJobClient.Schedule(() => SendAdminWelcomeMessageAsync(organization.Id, newAdminUserId, actorUserId),
            TimeSpan.FromSeconds(1));
    }

    const string CustomizeMessage =
        "You can customize how I fit in with your organization and configure how I respond when tracking conversations.";

    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task SendAdminWelcomeMessageAsync(int organizationId, string newAdminUserId, string actorUserId)
    {
        var organization = await _organizationRepository.GetAsync(organizationId);
        if (organization is null)
        {
            // We're done here.
            return;
        }

        // If api token is currently null, throw an exception and let the Hangfire retry logic try again later.
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            throw new InvalidOperationException(
                $"Cannot send the admin welcome message to {newAdminUserId} because organization {organizationId} does not yet have an API Token.");
        }

        var actorMention = SlackFormatter.UserMentionSyntax(actorUserId);

        var message = $"{actorMention} added you to the Administrators role for your workspace’s Abbot. {CustomizeMessage}";

        var messageRequest = AdminWelcomeHandler.GetAdminWelcomeMessageBlocks(
            message,
            newAdminUserId,
            _urlGenerator.InviteUsersPage(),
            _urlGenerator.OrganizationSettingsPage(),
            includeReminderButton: true);

        var response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest);
        if (!response.Ok)
        {
            Log.ErrorSendingDirectMessage(
                newAdminUserId,
                organization.Name,
                organization.PlatformId,
                organization.Id,
                response.ToString());
        }
    }
}

public static partial class BackgroundSlackClientLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message =
            "About to send welcome message (Recipient: {PlatformUserId}, PlatformId: {PlatformId}, Org Name: {OrganizationName})")]
    public static partial void AboutToSendWelcomeMessage(
        this ILogger<BackgroundSlackClient> logger,
        string platformUserId,
        string platformId,
        string? organizationName);
}
