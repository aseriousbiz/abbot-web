using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using Hangfire;
using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Slack.BotFramework;
using Serious.Slack.Events;

namespace Serious.Abbot.BotFramework;

/// <summary>
/// Enqueues a Slack <see cref="IEventEnvelope{EventBody}"/> to be processed
/// in the background. Ensure that a given event is only processed once.
/// </summary>
public class SlackEventQueueClient : IEventQueueClient
{
    static readonly ILogger<SlackEventQueueClient> Log = ApplicationLoggerFactory.CreateLogger<SlackEventQueueClient>();

    /// <summary>
    /// Event types that should be published to the bus instead of being added to SlackEvents.
    /// The key for each entry is the event type that should be published to the bus.
    /// The value is the name of the queue the event should be sent to.
    /// </summary>
    public static readonly Dictionary<string, string> BusEventRouting = new()
    {
        { "user_change", "slack-event--user-change" }
    };

    readonly AbbotContext _db;
    readonly IDataProtectionProvider _dataProtectionProvider;
    readonly IBackgroundJobClient _backgroundJobClient;
    readonly ISendEndpointProvider _sendEndpointProvider;
    readonly IOptionsMonitor<AbbotOptions> _abbotOptions;

    static readonly Counter<long> EnqueueCounter = AbbotTelemetry.Meter.CreateCounter<long>(
        "slack.event.enqueued.count",
        "events",
        "The number of events enqueued, by type and team (if available)");

    public SlackEventQueueClient(
        AbbotContext db,
        IDataProtectionProvider dataProtectionProvider,
        IBackgroundJobClient backgroundJobClient,
        ISendEndpointProvider sendEndpointProvider,
        IOptionsMonitor<AbbotOptions> abbotOptions)
    {
        _db = db;
        _dataProtectionProvider = dataProtectionProvider;
        _backgroundJobClient = backgroundJobClient;
        _sendEndpointProvider = sendEndpointProvider;
        _abbotOptions = abbotOptions;
    }

    public async Task EnqueueEventAsync(IEventEnvelope<EventBody> envelope, string eventBody, int? integrationId, int retryNumber)
    {
        var metricTags = new TagList()
        {
            { "category", "event" },
            { "envelope_type", envelope.Type },
            { "event_type", envelope.Event.Type },
            { "slack_team", envelope.TeamId },
            { "is_custom", integrationId is not null },
            { "retry_number", retryNumber },
        };

        // A special case for MT-enabled messages
        // These never hit the database
        if (_abbotOptions.CurrentValue.UseBusForSlackEvents && BusEventRouting.TryGetValue(envelope.Event.Type, out var queueName))
        {
            // Figure out where we're publishing
            var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri($"queue:{queueName}"));
            Log.EventRoutedToBus(envelope.Event.Type, queueName);

            // Pack it up and board the bus.
            var message = new SlackEventReceived()
            {
                Envelope = envelope,
                EventType = envelope.Event.Type,
                IntegrationId = (Id<Integration>?)integrationId,
                RetryNumber = retryNumber,
                TeamId = envelope.TeamId.Require(),
            };

            await endpoint.Send(message);

            metricTags.Add("delivery", "bus");
            metricTags.Add("bus_queue", queueName);
            EnqueueCounter.Add(1, metricTags);
            return;
        }
        metricTags.Add("delivery", "hangfire");

        var slackEvent = new SlackEvent
        {
            EventId = envelope.EventId,
            EventType = envelope.Event.Type,
            Content = new SecretString(eventBody, _dataProtectionProvider),
            Created = DateTime.UtcNow,
            TeamId = envelope.TeamId.Require(),
            AppId = envelope.ApiAppId,
        };

        await _db.SlackEvents.AddAsync(slackEvent);
        try
        {
            await _db.SaveChangesAsync();
            _backgroundJobClient.Enqueue<SlackEventProcessor>(
                p => p.ProcessEventAsync(
                    slackEvent.Id,
                    slackEvent.EventType,
                    null /* Hangfire fills this in */,
                    integrationId,
                    CancellationToken.None /* And this too */));
            EnqueueCounter.Add(1, metricTags);
        }
        catch (DbUpdateException e) when (e.GetDatabaseError() is UniqueConstraintError { ConstraintName: "IX_SlackEvents_EventId" })
        {
            // This may happen if for some reason, we take longer than three seconds to respond to Slack.
            // At this point, we should probably log it and then move on.
            Log.ExceptionEnqueueingEvent(e, envelope.EventId);
        }
    }
}

public static partial class SlackEventQueueClientLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Received a duplicate event. EventId: {SlackEventId}")]
    public static partial void ExceptionEnqueueingEvent(
        this ILogger<SlackEventQueueClient> logger,
        Exception exception,
        string slackEventId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Routing '{EventType}' to Queue '{QueueName}'")]
    public static partial void EventRoutedToBus(
        this ILogger<SlackEventQueueClient> logger,
        string eventType,
        string queueName);
}
