// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters.Slack;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Refit;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Configuration;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Slack.Abstractions;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Events;
using Serious.Slack.Payloads;
using Activity = Microsoft.Bot.Schema.Activity;
using IBot = Microsoft.Bot.Builder.IBot;

namespace Serious.Slack.BotFramework;

/// <summary>
/// Represents a bot adapter that can connect a bot to a Slack instance.
/// </summary>
/// <remarks>The bot adapter encapsulates authentication processes and sends
/// activities to and receives activities from the Bot Connector Service. When your
/// bot receives an activity, the adapter creates a context object, passes it to your
/// bot's application logic, and sends responses back to the user's channel.
/// <para>Use <see cref="BotAdapter.Use"/> to add <see cref="IMiddleware"/> objects
/// to your adapter’s middleware collection. The adapter processes and directs
/// incoming activities in through the bot middleware pipeline to your bot’s logic
/// and then back out again. As each activity flows in and out of the bot, each piece
/// of middleware can inspect or act upon the activity, both before and after the bot
/// logic runs.</para>
/// </remarks>
/// <seealso cref="ITurnContext"/>
/// <seealso cref="IActivity"/>
/// <seealso cref="Microsoft.Bot.Builder.IBot"/>
/// <seealso cref="IMiddleware"/>
public class SlackAdapter : BotAdapter, IBotFrameworkAdapter
{
    static readonly ILogger<SlackAdapter> Log = ApplicationLoggerFactory.CreateLogger<SlackAdapter>();

    /// <summary>
    /// For the event callback types, these are the event types we are interested in.
    /// </summary>
    static readonly HashSet<string> EventTypeWhitelist = new()
    {
        "message",
        "app_mention",
        "app_uninstalled",
        "app_home_opened",
        "channel_rename",
        "team_join",
        "team_rename",
        "user_change",
    };

    static readonly Counter<long> EventReceiveCountMetric = AbbotTelemetry.Meter.CreateCounter<long>(
        "slack.events.receive.count",
        "events",
        "The number of events received, by type and team (if available)");
    static readonly Counter<long> EventProcessCountMetric = AbbotTelemetry.Meter.CreateCounter<long>(
        "slack.events.process.count",
        "events",
        "The number of events processed, by type and team (if available)");
    static readonly Histogram<long> EventProcessDurationMetric = AbbotTelemetry.Meter.CreateHistogram<long>(
        "slack.events.process.duration",
        "milliseconds",
        "The processing duration for an event, by type and team (if available)");


    /// <summary>
    /// These are reactions we want to log.
    /// </summary>
    static readonly HashSet<string> ReactionsToLog = new(StringComparer.OrdinalIgnoreCase)
    {
        "eyes",
        "ticket",
        "white_check_mark",
    };

    /// <summary>
    /// We only accept events with a type 'message' if it has no subtype or a subtype matching any of the members
    /// of this list.
    /// </summary>
    /// <remarks>
    /// Several Slack events come with two types of events:
    /// 1. The event itself
    /// 2. A 'message' event that is sent to the channel to inform members of the channel about the event.
    ///
    /// For example, when a user is added to a channel, two events are fired:
    /// 1. A 'member_joined_channel' event, to inform bots/apps that the user has joined the channel.
    /// 2. A 'message' event with the sub-type 'channel_join' and the text "&lt;@U12345&gt; has joined the channel".
    ///
    /// The latter is designed to allow a client that only processes 'message' events to render useful events in a
    /// way that users expect. We ignore these message subtypes because we process the specific event types instead.
    ///
    /// But in some cases, we don't receive a proper event, so we need to allow the message subtype to be processed.
    /// </remarks>
    public static bool IsAllowedMessageEvent(MessageEvent messageEvent)
    {
        // When Abbot replies, the event should have a subtype of "bot_message". But when it's a DM,
        // the subtype is null. But the "bot_id" is set, which is only true for bot messages.
        if (messageEvent.SubType is null && messageEvent.BotId is { Length: > 0 })
        {
            return false;
        }

        if (messageEvent is BotMessageEvent { BotProfile.IsWorkflowBot: true })
        {
            return true;
        }

        return messageEvent.SubType is null
            or { Length: 0 }
            or "channel_convert_to_private"
            or "message_deleted"
            or "channel_name"
            or "file_share";
    }

    readonly IOptionsMonitor<SlackEventOptions> _slackEventOptions;
    readonly SlackEventDeduplicator _deduplicator;
    readonly ISlackApiClient _slackClient;
    // ReSharper disable once NotAccessedField.Local
    readonly IEventQueueClient _eventQueueClient;
    readonly ISensitiveLogDataProtector _dataProtector;

    protected ILogger<SlackAdapter> Logger { get; }

    public SlackAdapter(
        IOptionsMonitor<SlackEventOptions> slackEventOptions,
        SlackEventDeduplicator deduplicator,
        ISlackApiClient slackClient,
        IEventQueueClient eventQueueClient,
        ISensitiveLogDataProtector dataProtector,
        ILogger<SlackAdapter> logger)
    {
        _slackEventOptions = slackEventOptions;
        _deduplicator = deduplicator;
        _slackClient = slackClient;
        _eventQueueClient = eventQueueClient;
        _dataProtector = dataProtector;
        Logger = logger;
    }

    /// <summary>
    /// Standard BotBuilder adapter method to send a message from the bot to the messaging API.
    /// </summary>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="activities">An array of outgoing activities to be sent back to the messaging API.</param>
    /// <param name="cancellationToken">A cancellation token for the task.</param>
    /// <returns>An array of <see cref="ResourceResponse"/> objects containing the IDs that Slack assigned to the sent messages.</returns>
    public override async Task<ResourceResponse[]> SendActivitiesAsync(
        ITurnContext turnContext,
        Activity[] activities,
        CancellationToken cancellationToken)
    {
        var responses = new List<ResourceResponse>();

        foreach (var activity in activities)
        {
            if (activity.Type is ActivityTypes.Message)
            {
                await SendMessageActivity(activity, responses);
            }
            else
            {
                Logger.UnsupportedActivityTypes(activity.Type);
            }
        }

        return responses.ToArray();
    }

    async Task SendMessageActivity(Activity activity, ICollection<ResourceResponse> responses)
    {
        var channelData = SlackHelper.ActivityToSlack(activity);
        var apiToken = channelData.ApiToken.Reveal();
        ApiResponse response = channelData.EphemeralUser is null
            ? await _slackClient.PostMessageWithRetryAsync(apiToken, channelData.Message)
                .ConfigureAwait(false)
            : await _slackClient.PostEphemeralMessageWithRetryAsync(
                    apiToken,
                    new EphemeralMessageRequest(channelData.Message)
                    {
                        User = channelData.EphemeralUser
                    })
                .ConfigureAwait(false);

        if (!response.Ok)
        {
            throw new InvalidOperationException(response.ToString());
        }

        var (timestamp, conversation) = response switch
        {
            MessageResponse { Body: { } body } => (body.Timestamp, new ConversationAccount
            {
                Id = body.Channel
            }),
            EphemeralMessageResponse ephemeralResponse => (ephemeralResponse.Body, null),
            _ => throw new UnreachableException()
        };

        var resourceResponse = new ActivityResourceResponse
        {
            Id = timestamp,
            ActivityId = timestamp,
            Conversation = conversation
        };

        responses.Add(resourceResponse);
    }

    /// <summary>
    /// Standard BotBuilder adapter method to update a previous message with new content.
    /// </summary>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="activity">The updated activity in the form '{id: `id of activity to update`, ...}'.</param>
    /// <param name="cancellationToken">A cancellation token for the task.</param>
    /// <returns>A resource response with the Id of the updated activity.</returns>
    public override async Task<ResourceResponse> UpdateActivityAsync(
        ITurnContext turnContext,
        Activity activity,
        CancellationToken cancellationToken)
    {
        if (activity.ChannelData is not MessageChannelData(var apiToken, var messageRequest, var responseUrl))
        {
            throw new InvalidOperationException(
                $"To update the message, set the activity channel data to {nameof(MessageChannelData)}");
        }

        ApiResponse? response;
        ResponseUrlUpdateMessageRequest? updateRequest = null;
        try
        {
            if (activity.Id is not null)
            {
                response = await _slackClient.UpdateMessageAsync(apiToken.Reveal(), messageRequest)
                    .ConfigureAwait(false);
            }
            else if (responseUrl is not null)
            {
                updateRequest = ResponseUrlUpdateMessageRequest.FromMessageRequest(messageRequest);
                response = await _slackClient.GetResponseUrlClient(responseUrl)
                    .UpdateAsync(apiToken.Reveal(), updateRequest).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException(
                    "Please set the activity Id or the responseUrl in order to update the message");
            }
        }
        catch (ApiException e)
        {
#if DEBUG
#pragma warning disable CA1848
            Logger.LogError(e, "The message was {MessageJson}", JsonConvert.SerializeObject(updateRequest));
#pragma warning restore
#endif

            response = (e.Content is { Length: > 0 } content
                           ? DeserializeObject<ApiResponse>(content)
                           : null)
                       ?? new ApiResponse
                       {
                           Ok = false,
                           Error = e.ToString()
                       };
        }

        if (!response.Ok)
        {
            throw new InvalidOperationException(response.ToString());
        }

        return new ResourceResponse
        {
            Id = activity.Id,
        };
    }

    /// <summary>
    /// Standard BotBuilder adapter method to delete a previous message.
    /// </summary>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="reference">An object in the form "{activityId: `id of message to delete`, conversation: { id: `id of slack channel`}}".</param>
    /// <param name="cancellationToken">A cancellation token for the task.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public override async Task DeleteActivityAsync(
        ITurnContext turnContext,
        ConversationReference reference,
        CancellationToken cancellationToken)
    {
        (SecretString? apiToken, var uri) =
            reference.Conversation.Properties["ChannelData"]?.ToObject<DeleteChannelData>()
            ?? throw new InvalidOperationException("No delete channel data provided");

        ApiResponse? response;
        if (uri is not null)
        {
            response = await _slackClient.GetResponseUrlClient(uri)
                .DeleteAsync(apiToken.Reveal(), new ResponseUrlDeleteMessageRequest());
        }
        else
        {
            var messageId = reference.ActivityId.Require();
            var channelId = reference.ChannelId.Require();

            response = await _slackClient.DeleteMessageAsync(apiToken.Reveal(), channelId, messageId);
        }

        if (!response.Ok)
        {
            throw new InvalidOperationException(response.ToString());
        }
    }

    /// <summary>
    /// Sends a proactive message from the bot to a conversation.
    /// </summary>
    /// <param name="claimsIdentity">A <see cref="ClaimsIdentity"/> for the conversation.</param>
    /// <param name="reference">A reference to the conversation to continue.</param>
    /// <param name="callback">The method to call for the resulting bot turn.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    /// <remarks>Call this method to proactively send a message to a conversation.
    /// Most _channels require a user to initialize a conversation with a bot
    /// before the bot can send activities to the user.
    /// <para>This method registers the following services for the turn.<list type="bullet">
    /// <item><description><see cref="IIdentity"/> (key = "BotIdentity"), a claims claimsIdentity for the bot.
    /// </description></item>
    /// </list></para>
    /// </remarks>
    /// <seealso cref="BotAdapter.RunPipelineAsync(ITurnContext, BotCallbackHandler, CancellationToken)"/>
    public override async Task ContinueConversationAsync(
        ClaimsIdentity claimsIdentity,
        ConversationReference reference,
        BotCallbackHandler callback,
        CancellationToken cancellationToken)
    {
        using var context = new TurnContext(this, reference.GetContinuationActivity());
        context.TurnState.Add<IIdentity>(BotIdentityKey, claimsIdentity);
        context.TurnState.Add(callback);
        await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IActionResult> ProcessAsync(
        string requestBody,
        string requestContentType,
        IBot bot,
        int? integrationId,
        int retryNumber,
        string? retryReason,
        CancellationToken cancellationToken)
    {
        if (requestContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            var incomingEvent = DeserializeObject<IElement>(requestBody);

            var metricTags = new TagList()
            {
                { "category", "event" },
                { "envelope_type", incomingEvent?.Type },
                { "slack_team", incomingEvent?.TeamId },
                { "is_custom", integrationId is not null },
                { "retry_number", retryNumber },
                { "retry_reason", retryReason },
            };

            switch (incomingEvent)
            {
                case UrlVerificationEvent verificationEvent:
                    EventReceiveCountMetric.Add(1,
                        metricTags); // We emit this here because we add a tag in one of the cases

                    return Content(verificationEvent.Challenge);
                case AppRateLimitedEvent rateLimitedEvent:
                    {
                        var dateRateLimitedStarted = DateTimeOffset.FromUnixTimeSeconds(1518467820)
                            .ToString("o", DateTimeFormatInfo.InvariantInfo);

                        Logger.BotRateLimited(rateLimitedEvent.TeamId, dateRateLimitedStarted);
                        EventReceiveCountMetric.Add(1,
                            metricTags); // We emit this here because we add a tag in one of the cases

                        return Content("Ok, we'll deal with it.");
                    }
                case IEventEnvelope<EventBody> eventEnvelope:
                    var disposition = GetEventDisposition(eventEnvelope);
                    metricTags.Add("event_type", eventEnvelope.Event.Type);
                    metricTags.Add("disposition", disposition.ToString());
                    EventReceiveCountMetric.Add(1,
                        metricTags); // We emit this here because we add a tag in one of the cases

                    if (disposition is EventDisposition.Allowed)
                    {
                        // this is an event api post, we need to enqueue it.
                        // But for now, but before that, we need to migrate to a better database.
                        await _eventQueueClient.EnqueueEventAsync(
                            eventEnvelope,
                            requestBody,
                            integrationId,
                            retryNumber);

                        return Content("Don't call us, we'll call you.");
                    }

                    // Seen this key already or it doesn't meet our filter criteria.
                    return Content("Thanks, we got it.");
            }
        }

        var activity = GetActivityFromFormPost(requestContentType, requestBody);

        // If we get here, it's something we handle inline.
        var activityTags = new TagList()
        {
            { "category", "activity" },
            { "activity_type", activity?.Type },
            {
                "slack_team", activity?.Value is IPayload payload
                    ? payload.TeamId
                    : null
            },
            { "disposition", EventDisposition.Allowed.ToString() }
        };

        EventReceiveCountMetric.Add(1, activityTags);
        EventProcessCountMetric.Add(1, activityTags);
        using var _ = EventProcessDurationMetric.Time(activityTags);

        // As per official Slack API docs, some additional request types may be received that can be ignored
        // but we should respond with a 200 status code
        // https://api.slack.com/interactivity/slash-commands
        if (activity is null)
        {
            return Content("Unable to transform request / payload into Activity. Possible unrecognized request type");
        }

        async Task ExecutePipelineAsync(ITurnContext turnContext)
        {
            await RunPipelineAsync(turnContext, bot.OnTurnAsync, cancellationToken)
                .ConfigureAwait(false);
        }

        var turnContext = new TurnContext(this, activity);
        if (integrationId != null)
        {
            turnContext.SetIntegrationId(integrationId.Value);
        }

        return new ActivityResult(turnContext, ExecutePipelineAsync);
    }

    T? DeserializeObject<T>(string value)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(value);
        }
        catch (JsonException e)
        {
            var protectedValue = _dataProtector.Protect(value);

            // In general, we never want to log content from customers. This is why we encrypt the event JSON when
            // enqueuing processing of an incoming Slack event. But in this case, we can't even deserialize the
            // incoming JSON. This is a pretty exceptional case so we'll log it so we can fix it and rely on log
            // expiration to not store it indefinitely.
            // We can remove this when it stops happening: @haacked 2021-05-25
            throw SlackEventDeserializationException.Create(value, typeof(T), protectedValue, e);
        }
    }

    // Determines whether or not we should handle the event. For now, the only time we should NOT handle the event
    // is if we've already seen it. Later on, we may want to add more filtering criteria.
    EventDisposition GetEventDisposition(IEventEnvelope<EventBody> envelope)
    {
        var eventOptions = _slackEventOptions.CurrentValue.GetEventConfiguration(envelope.Event.Type);

        using var scope = Log.ReceivedSlackEvent(
            eventId: envelope.EventId,
            envelopeType: envelope.Type,
            eventType: envelope.Event.Type,
            platformId: envelope.TeamId.Require(),
            platformUserId: envelope.Event.User);

        if (envelope is IEventEnvelope<MessageEvent> msg && !IsAllowedMessageEvent(msg.Event))
        {
            return EventDisposition.IgnoredByDesign;
        }

        if (eventOptions.Ignored)
        {
            return EventDisposition.IgnoredByConfig;
        }

        if (_deduplicator.IsDuplicate(envelope.Event))
        {
            return EventDisposition.DuplicatePayload;
        }

        bool isAllowedType = EventTypeWhitelist.Contains(envelope.Event.Type);

        if (envelope.Event is MessageEvent messageEvent)
        {
            Logger.MessageEventReceived(
                messageId: messageEvent.Timestamp,
                threadId: messageEvent.ThreadTs,
                subType: messageEvent.SubType,
                isAllowedType);
        }
        else if (envelope.Event is ReactionEvent reactionEvent && ReactionsToLog.Contains(reactionEvent.Reaction))
        {
            Logger.ReactionEventReceived(
                reaction: reactionEvent.Reaction,
                messageId: reactionEvent.Item.Timestamp,
                isAllowedType);
        }
        else
        {
            Logger.EventReceived(isAllowedType);
        }

        return EventDisposition.Allowed;
    }

    public async Task ProcessEventAsync(
        IEventEnvelope<EventBody> eventEnvelope,
        IBot bot,
        int? integrationId,
        CancellationToken cancellationToken)
    {
        using var scope = Log.BeginProcessingSlackEvent(
            eventId: eventEnvelope.EventId,
            envelopeType: eventEnvelope.Type,
            eventType: eventEnvelope.Event.Type,
            platformId: eventEnvelope.TeamId.Require(),
            platformUserId: eventEnvelope.Event.User);

        var metricTags = new TagList()
        {
            { "category", "event" },
            { "envelope_type", eventEnvelope.Type },
            { "event_type", eventEnvelope.Event.Type },
            { "slack_team", eventEnvelope.TeamId }
        };

        EventProcessCountMetric.Add(1, metricTags);
        using var _ = EventProcessDurationMetric.Time(metricTags);

        var activity = SlackHelper.EventToActivity(eventEnvelope);
        using var turnContext = new TurnContext(this, activity);
        if (integrationId is not null)
        {
            turnContext.SetIntegrationId(integrationId.Value);
        }

        turnContext.SetSlackEventId(SlackEventInfo.FromEventEnvelope(eventEnvelope));
        OnTurnError = null;

        try
        {
            await RunPipelineAsync(turnContext, bot.OnTurnAsync, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // Give the implementer a chance to provide their own error handling
            await HandleUnhandledExceptionAsync(turnContext, e);
            throw;
        }
    }

    protected virtual Task HandleUnhandledExceptionAsync(ITurnContext turnContext, Exception exception)
    {
        return Task.CompletedTask;
    }

    static ContentResult Content(string content)
    {
        return new ContentResult
        {
            Content = content,
            StatusCode = (int)HttpStatusCode.OK,
            ContentType = "text/plain"
        };
    }

    Activity? GetActivityFromFormPost(string contentType, string body)
    {
        if (!contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var postValues = SlackHelper.QueryStringToDictionary(body);

        if (postValues.TryGetValue("payload", out var payloadJson))
        {
            var payload = DeserializeObject<IPayload>(payloadJson);
            if (payload is null)
            {
                throw new InvalidOperationException(
                    "Received a application/x-www-form-urlencoded request from Slack with a `payload` key that could not be deserialized to an InteractionPayload. This shouldn't be possible!");
            }

            Logger.PayloadReceived(payload.GetType().Name);
            return SlackHelper.PayloadToActivity(payload);
        }

        if (postValues.ContainsKey("command"))
        {
            var serializedPayload = JsonConvert.SerializeObject(postValues);
            var payload = DeserializeObject<CommandPayload>(serializedPayload);
            if (payload is null)
            {
                throw new InvalidOperationException(
                    "Received a application/x-www-form-urlencoded request from Slack with a `command` key that could not be deserialized to a CommandPayload. This shouldn't be possible!");
            }

            return SlackHelper.CommandToActivity(payload);
        }

        return null;
    }

    enum EventDisposition
    {
        Allowed,
        IgnoredByDesign,
        IgnoredByConfig,
        DuplicatePayload,
    }
}

/// <summary>
/// Top-level information about the Slack event we want to store in the turnContext so we can include it in logging.
/// Each property should be a custom property in our logs.
/// </summary>
public readonly record struct SlackEventInfo(
    string SlackEventId,
    string EventType,
    string TeamId,
    SlackEventDetails EventDetails)
{
    const string NullValue = "<<null>>";

    public static SlackEventInfo FromEventEnvelope(IEventEnvelope<EventBody> eventEnvelope)
    {
        var innerEvent = eventEnvelope.Event;
        return new SlackEventInfo(
            eventEnvelope.EventId,
            eventEnvelope.Type,
            eventEnvelope.TeamId.Require(),
            new SlackEventDetails(
                innerEvent.Type,
                innerEvent is MessageEvent messageEvent
                    ? messageEvent.SubType
                    : null,
                eventEnvelope.GetChannelId() ?? NullValue,
                innerEvent.User ?? NullValue,
                innerEvent.Timestamp ?? NullValue,
                eventEnvelope.EventTime,
                innerEvent.EventTimestamp,
                innerEvent.ThreadTs ?? NullValue));
    }
};

/// <summary>
/// Detailed information about the slack event that can all be stored in a single custom property.
/// </summary>
public readonly record struct SlackEventDetails(
    string Type,
    string? Subtype,
    string Channel,
    string User,
    string TimeStamp,
    long EventTime,
    string EventTimeStamp,
    string ThreadTimestamp);

public static partial class SlackAdapterLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Unsupported Activity Type: '{ActivityType}'. Only Activities of type 'Message' are supported.")]
    public static partial void UnsupportedActivityTypes(this ILogger logger, string activityType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Bot is being rate-limited for the team: '{TeamId}'. Occurred on {DateRateLimitingStarted}.")]
    public static partial void BotRateLimited(this ILogger logger, string teamId, string dateRateLimitingStarted);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Received Event. Allowed: {Allowed}")]
    public static partial void EventReceived(
        this ILogger logger,
        bool allowed);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message =
            "Received Message {MessageId} (Thread  = {ThreadId}, Subtype = {SubType}). Allowed: {Allowed}")]
    public static partial void MessageEventReceived(
        this ILogger logger,
        string? messageId,
        string? threadId,
        string? subType,
        bool allowed);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Interaction {PayloadType} received from Slack.")]
    public static partial void PayloadReceived(this ILogger logger, string payloadType);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "Slack API reported an error: '{SlackError}'")]
    public static partial void SlackErrorReceived(this ILogger logger, string slackError);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Received Reaction {Reaction} on Message {MessageId}. Allowed: {Allowed}")]
    public static partial void ReactionEventReceived(
        this ILogger logger,
        string reaction,
        string? messageId,
        bool allowed);

    static readonly Func<ILogger, string, string, string, string, string?, IDisposable?> ReceivedSlackEventScope =
        LoggerMessage.DefineScope<string, string, string, string, string?>(
            "Received Slack Event ({SlackEventId}): {EnvelopeType}.{EventType} {PlatformId} {PlatformUserId}");

    public static IDisposable? ReceivedSlackEvent(this ILogger<SlackAdapter> logger, string eventId,
        string envelopeType, string eventType, string platformId, string? platformUserId) =>
        ReceivedSlackEventScope(logger, eventId, envelopeType, eventType, platformId, platformUserId);

    static readonly Func<ILogger, string, string, string, string, string?, IDisposable?> ProcessingSlackEventScope =
        LoggerMessage.DefineScope<string, string, string, string, string?>(
            "Processing Slack Event ({SlackEventId}): {EnvelopeType}.{EventType} {PlatformId} {PlatformUserId}");

    public static IDisposable? BeginProcessingSlackEvent(this ILogger<SlackAdapter> logger, string eventId,
        string envelopeType, string eventType, string platformId, string? platformUserId) =>
        ProcessingSlackEventScope(logger, eventId, envelopeType, eventType, platformId, platformUserId);
}
