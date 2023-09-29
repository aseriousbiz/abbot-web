using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Messaging;
using Serious.Abbot.Routing;
using Serious.Abbot.Scripting;
using Serious.Abbot.Security;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Handles interactions with the admin welcome message .
/// </summary>
public class AdminWelcomeHandler : IHandler
{
    static readonly ILogger<AdminWelcomeHandler> Log = ApplicationLoggerFactory.CreateLogger<AdminWelcomeHandler>();

    readonly IUrlGenerator _urlGenerator;
    readonly AbbotContext _db;
    readonly ISlackApiClient _slackApiClient;
    readonly IRoleManager _roleManager;
    readonly IBackgroundJobClient _jobClient;

    public AdminWelcomeHandler(
        IUrlGenerator urlGenerator,
        AbbotContext db,
        ISlackApiClient slackApiClient,
        IRoleManager roleManager,
        IBackgroundJobClient jobClient)
    {
        _urlGenerator = urlGenerator;
        _db = db;
        _slackApiClient = slackApiClient;
        _roleManager = roleManager;
        _jobClient = jobClient;
    }

    /// <summary>
    /// Handles the "Remind me later" and "Add Administrators" button clicks.
    /// </summary>
    /// <param name="platformMessage">The incoming message.</param>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        var action = platformMessage.Payload.InteractionInfo?.Arguments;

        if (action is "remind")
        {
            // Strip the blocks from the previous message.
            if (platformMessage is { Payload.InteractionInfo: { ResponseUrl: { } responseUrl, SourceMessage.Text: { Length: > 0 } originalMessage } })
            {
                var update = new RichActivity(originalMessage)
                {
                    ResponseUrl = responseUrl
                };
                // Respond with the original message, but without the blocks.
                await platformMessage.Responder.UpdateActivityAsync(update);
            }

            // Set up a reminder.
            EnqueueAdminReminderMessage(
                platformMessage.Organization,
                platformMessage.From,
                TimeSpan.FromDays(1));

            // Add a response
            await platformMessage.Responder.SendActivityAsync("Ok! I’ll remind you tomorrow about customizing me.");
        }
        else if (action is "admins" && platformMessage.TriggerId is { } triggerId)
        {
            var existingAdmins =
                await _roleManager.GetMembersInRoleAsync(Roles.Administrator, platformMessage.Organization);
            var view = AdminModalHandler.CreateAdministratorsModal(existingAdmins, showAppHomeTabMessage: true);
            await platformMessage.Responder.OpenModalAsync(triggerId, view);
        }
    }

    void EnqueueAdminReminderMessage(Organization organization, Member admin, TimeSpan reminderDelay)
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

        var slackUserId = admin.User.PlatformUserId;

        _jobClient.Schedule(() => SendAdminReminderMessageAsync(organization.Id, slackUserId), reminderDelay);
    }

    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task SendAdminReminderMessageAsync(int organizationId, string newAdminUserId)
    {
        var organization = await _db.Organizations.FindAsync(organizationId);
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

        var message = "You asked me to remind you to add additional Administrators and to customize me to fit in with your organization (such as configuring auto-replies).";

        var messageRequest = GetAdminWelcomeMessageBlocks(
            message,
            newAdminUserId,
            _urlGenerator.InviteUsersPage(),
            _urlGenerator.OrganizationSettingsPage(),
            includeReminderButton: false);

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

    public static MessageRequest GetAdminWelcomeMessageBlocks(
        string message,
        string slackUserId,
        Uri invitationUrl,
        Uri settingsUrl,
        bool includeReminderButton = false)
    {
        var actionButtons = new List<IActionElement>
        {
            new ButtonElement(":key: Manage Administrators", Value: "admins")
            {
                Style = ButtonStyle.Primary
            },
            new ButtonElement(":envelope: Invite Users", Value: "invite")
            {
                Url = invitationUrl
            },
            new ButtonElement(":gear: Customize Abbot", Value: "customize")
            {
                Url = settingsUrl
            },
        };

        if (includeReminderButton)
        {
            actionButtons.Add(new ButtonElement(":hourglass: Remind me tomorrow", Value: "remind"));
        }

        var messageRequest = new MessageRequest(slackUserId, message)
        {
            Blocks = new ILayoutBlock[]
            {
                new Section(new MrkdwnText(message)),
                new Actions(actionButtons.ToArray())
                {
                    BlockId = new InteractionCallbackInfo(nameof(AdminWelcomeHandler))
                }
            }
        };
        return messageRequest;
    }
}

public static partial class AdminWelcomeHandlerLoggingExtensions
{
    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Error,
        Message =
            "Failed to send direct message to member {PlatformUserId} (Name: {OrganizationName}, PlatformId: {PlatformId}, Id: {OrganizationId}, Error: {ErrorMessage})")]
    public static partial void ErrorSendingDirectMessage(
        this ILogger logger,
        string platformUserId,
        string? organizationName,
        string platformId,
        int organizationId,
        string? errorMessage);
}
