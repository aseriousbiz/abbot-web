using System.Globalization;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Events;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Outputs;
using Serious.Abbot.Repositories;
using Serious.Slack;
using Serious.Slack.Events;

namespace Serious.Abbot.PayloadHandlers;

public class SharedChannelInviteAcceptedHandler : IPayloadHandler<SharedChannelInviteAccepted>
{
    readonly SharedChannelInviteHandler _handler;

    public SharedChannelInviteAcceptedHandler(SharedChannelInviteHandler handler)
    {
        _handler = handler;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<SharedChannelInviteAccepted> platformEvent)
    {
        await _handler.ResumePlaybookAsync(
            platformEvent.Payload,
            "accepted",
            platformEvent.Payload.AcceptingUser,
            platformEvent.Payload.AcceptingUser?.TeamId,
            platformEvent.Organization);
    }
}

public class SharedChannelInviteApprovedHandler : IPayloadHandler<SharedChannelInviteApproved>
{
    readonly SharedChannelInviteHandler _handler;

    public SharedChannelInviteApprovedHandler(SharedChannelInviteHandler handler)
    {
        _handler = handler;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<SharedChannelInviteApproved> platformEvent)
    {
        await _handler.ResumePlaybookAsync(
            platformEvent.Payload,
            "accepted", // Not sure if the difference between accepted or approved is important
            platformEvent.Payload.ApprovingUser,
            platformEvent.Payload.ApprovingTeamId,
            platformEvent.Organization);
    }
}

public class SharedChannelInviteDeclinedHandler : IPayloadHandler<SharedChannelInviteDeclined>
{
    readonly SharedChannelInviteHandler _handler;
    readonly ILogger<SharedChannelInviteDeclinedHandler> _logger;

    public SharedChannelInviteDeclinedHandler(
        SharedChannelInviteHandler handler,
        ILogger<SharedChannelInviteDeclinedHandler> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<SharedChannelInviteDeclined> platformEvent)
    {
        _logger.ReceivedDeclineEvent(platformEvent.Payload.Invite.Id);

        await _handler.ResumePlaybookAsync(platformEvent.Payload,
            "declined",
            platformEvent.Payload.DecliningUser,
            platformEvent.Payload.DecliningTeamId,
            platformEvent.Organization);
    }
}

static partial class SharedChannelInviteDeclinedHandlerLoggingExtensions
{
    // We're not clear on whether Slack *ever* sends this event so lets log it to see if it ever shows up.
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Look at that! We received a `shared_channel_invite_declined` event for invite {InviteId}!")]
    public static partial void ReceivedDeclineEvent(
        this ILogger<SharedChannelInviteDeclinedHandler> logger,
        string inviteId);
}

public class SharedChannelInviteHandler
{
    readonly IPublishEndpoint _publishEndpoint;
    readonly ISettingsManager _settingsManager;

    public SharedChannelInviteHandler(IPublishEndpoint publishEndpoint, ISettingsManager settingsManager)
    {
        _publishEndpoint = publishEndpoint;
        _settingsManager = settingsManager;
    }

    public async Task ResumePlaybookAsync(
        SharedChannelInviteEvent sharedChannelInviteEvent,
        string invitationResponse,
        UserInfo? inviteeActor,
        string? inviteeTeamId,
        Organization organization)
    {
        var inviteId = sharedChannelInviteEvent.Invite.Id;

        var setting = await _settingsManager.GetAsync(
            SettingsScope.Organization(organization),
            GetInviteIdKey(inviteId));

        if (setting is null)
        {
            return;
        }

        var context = Arguments.Parse(setting.Value);

        await _publishEndpoint.Publish(
            new ResumeSuspendedStep(context.PlaybookRunId, context.Step)
            {
                SuspendState = new OutputsBuilder()
                    .SetInvitationResponse(invitationResponse, inviteeTeamId, ActorOutput.FromUserInfo(inviteeActor))
                    .Outputs,
            });
    }

    public async Task StoreInvitationContextAsync(StepContext context, string inviteId, User actor, Organization organization)
    {
        var arguments = new Arguments(context.PlaybookRun.CorrelationId, context.ActionReference, inviteId);

        await _settingsManager.SetAsync(
            SettingsScope.Organization(organization),
            GetInviteIdKey(inviteId),
            arguments,
            actor);
    }

    static string GetInviteIdKey(string inviteId) => $"Slack.Connect.Invitation:{inviteId}";

    public record Arguments(Guid PlaybookRunId, ActionReference Step, string InviteId)
    {
        public static Arguments Parse(string? arguments)
        {
            return arguments?.Split(',', 5) is [var runId, var seqId, var actionId, var actionIndex, var inviteId]
                ? new Arguments(
                    Guid.Parse(runId),
                    new(seqId, actionId, int.Parse(actionIndex, CultureInfo.InvariantCulture)),
                    inviteId)
                : throw new InvalidOperationException($"Could not parse arguments: {arguments}");
        }

        public override string ToString() => $"{PlaybookRunId},{Step.SequenceId},{Step.ActionId},{Step.ActionIndex},{InviteId}";

        public static implicit operator string(Arguments arguments) => arguments.ToString();
    }
}
