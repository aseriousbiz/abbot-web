using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Slack;

namespace Serious.Abbot.Eventing.Consumers;

public class CreateDefaultHubConsumer : IConsumer<CreateDefaultHub>
{
    readonly IUserRepository _userRepository;
    readonly IHubRepository _hubRepository;
    readonly ISlackApiClient _slackApiClient;
    readonly ISlackResolver _slackResolver;
    readonly ILogger<CreateDefaultHubConsumer> _logger;

    public CreateDefaultHubConsumer(IUserRepository userRepository, IHubRepository hubRepository, ISlackApiClient slackApiClient, ISlackResolver slackResolver, ILogger<CreateDefaultHubConsumer> logger)
    {
        _userRepository = userRepository;
        _hubRepository = hubRepository;
        _slackApiClient = slackApiClient;
        _slackResolver = slackResolver;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreateDefaultHub> context)
    {
        var organization = context.GetPayload<Organization>();
        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            _logger.OrganizationHasNoSlackApiToken();
            return;
        }

        // If the organization has a default hub, no-op
        if (organization.Settings.DefaultHubId is not null)
        {
            _logger.OrganizationHasDefaultHub();
            return;
        }

        var actor = await _userRepository.GetMemberByIdAsync(context.Message.ActorId);
        if (actor is null)
        {
            _logger.EntityNotFound(context.Message.ActorId);
            return;
        }

        async Task ReportErrorAsync(string error)
        {
            // We don't have anywhere to notify except the actor!
            var messageRequest = new MessageRequest(
                actor.Require().User.PlatformUserId,
                $"Sorry, I couldn't create the Hub channel you requested. Contact {WebConstants.SupportEmail} for assistance and give them this request ID: `{Activity.Current?.Id}`. {error}");
            await _slackApiClient.PostMessageWithRetryAsync(apiToken.Require(), messageRequest);
        }

        // Create the hub channel
        var request = new ConversationCreateRequest(context.Message.Name, isPrivate: false);
        var response = await _slackApiClient.Conversations.CreateConversationAsync(
            apiToken,
            request);

        if (!response.Ok)
        {
            // We check these conditions before publishing the message, but just in case...
            var message = response.Error switch
            {
                "name_taken" => $"The channel name {context.Message.Name} is already taken.",
                // There are several "invalid_name" error codes.
                var x when x.StartsWith("invalid_name", StringComparison.Ordinal) =>
                    $"The channel name {context.Message.Name} is invalid.",
                var x => $"Unknown error creating channel: `{x}`.",
            };
            _logger.ErrorCreatingHubChannel(context.Message.Name, response.Error);
            await ReportErrorAsync(message);
            return;
        }

        // Resolve the new room to make sure it's in the DB.
        var room = await _slackResolver.ResolveRoomAsync(response.Body.Id, organization, forceRefresh: true);
        if (room is null)
        {
            _logger.CouldNotResolveRoom(response.Body.Id, context.Message.Name);
            await ReportErrorAsync("An unexpected error occurred.");
            return;
        }
        using var roomScope = _logger.BeginRoomScope(room);

        // Invite the actor to the channel
        var inviteResponse = await _slackApiClient.Conversations.InviteUsersToConversationAsync(
            apiToken,
            new(room.PlatformRoomId, new[] { actor.User.PlatformUserId }));

        if (!inviteResponse.Ok)
        {
            _logger.ErrorInvitingActor(actor.User.PlatformUserId, inviteResponse.Error);
            // We can press on even if we fail to invite the user.
        }

        // Is it somehow already a hub?
        var hub = await _hubRepository.GetHubAsync(room);
        if (hub is null)
        {
            // No. That's what we expect. Make it a Hub
            _logger.CreatingHub();
            hub = await _hubRepository.CreateHubAsync(room.Name.Require(), room, actor);
        }

        // Make it the default hub
        await _hubRepository.SetDefaultHubAsync(hub, actor);

        // Publish a welcome notification to the Hub.
        _logger.PublishingWelcome();
        await context.Publish(new PublishRoomNotification()
        {
            OrganizationId = organization,
            RoomId = room,
            Notification = new(
                "👋",
                "Welcome to Abbot",
                "This is your default Hub. I'll publish useful and important notices here for you to read."),
        });
    }
}

public static partial class CreateHubChannelConsumerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Error creating Slack channel {ChannelName}: {SlackError}")]
    public static partial void ErrorCreatingHubChannel(this ILogger<CreateDefaultHubConsumer> logger, string channelName, string slackError);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Room {PlatformRoomId} ({ChannelName}) could not be resolved after creating it in Slack.")]
    public static partial void CouldNotResolveRoom(this ILogger<CreateDefaultHubConsumer> logger,
        string platformRoomId, string channelName);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Error inviting {ActorPlatformUserId} to Slack channel: {SlackError}")]
    public static partial void ErrorInvitingActor(this ILogger<CreateDefaultHubConsumer> logger,
        string actorPlatformUserId, string slackError);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Creating Hub for room.")]
    public static partial void CreatingHub(this ILogger<CreateDefaultHubConsumer> logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Publishing welcome notification.")]
    public static partial void PublishingWelcome(this ILogger<CreateDefaultHubConsumer> logger);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "The organization already has a default Hub.")]
    public static partial void OrganizationHasDefaultHub(this ILogger<CreateDefaultHubConsumer> logger);
}
