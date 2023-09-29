using System.ComponentModel;
using System.Threading;
using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Logging;
using Serious.Slack.BotFramework;
using Serious.Slack.Events;
using IBot = Microsoft.Bot.Builder.IBot;

namespace Serious.Abbot.BotFramework;

/// <summary>
/// Class that does the work to process an incoming Slack event in the background.
/// </summary>
public class SlackEventProcessor
{
    static readonly ILogger<SlackEventProcessor> Log = ApplicationLoggerFactory.CreateLogger<SlackEventProcessor>();

    readonly IBotFrameworkAdapter _slackAdapter;
    readonly IBot _bot;
    readonly IDbContextFactory<AbbotContext> _dbContextFactory;

    /// <summary>
    /// Constructs a <see cref="SlackEventProcessor"/>.
    /// </summary>
    /// <param name="slackAdapter">The implementation of <see cref="IBotFrameworkAdapter"/> used to handle Slack events.</param>
    /// <param name="bot">The bot.</param>
    /// <param name="dbContextFactory">The factory to create an <see cref="AbbotContext"/> instance to manage the event.</param>
    public SlackEventProcessor(
        IBotFrameworkAdapter slackAdapter,
        IBot bot,
        IDbContextFactory<AbbotContext> dbContextFactory)
    {
        _slackAdapter = slackAdapter;
        _bot = bot;
        _dbContextFactory = dbContextFactory;
    }

    [Obsolete("This method is only here to support Hangfire. Use the overload that takes an eventId.")]
    [Queue(HangfireQueueNames.HighPriority)]
    public async Task ProcessEventAsync(
        int id,
        PerformContext? performContext,
        int? integrationId,
        CancellationToken cancellationToken) =>
        await ProcessEventAsync(new Id<SlackEvent>(id), null, performContext, integrationId, cancellationToken);

    [Obsolete("The method is only here for handling outstanding Hangfire jobs. Remove it in the future.")]
    [Queue(HangfireQueueNames.Maintenance)]
    [DisplayName("Process Slack Event {1} - Low Priority")]
    public async Task ProcessLowPriorityEventAsync(
        int id,
        string? eventType,
        PerformContext? performContext,
        int? integrationId,
        CancellationToken cancellationToken) =>
        await ProcessEventAsync(new Id<SlackEvent>(id), eventType, performContext, integrationId, cancellationToken);

    /// <summary>
    /// Given the primary key to a <see cref="SlackEvent"/>, retrieves the event from the database and
    /// runs the bot's logic to process the event using the <see cref="IBotFrameworkAdapter"/>.
    /// </summary>
    /// <param name="id">The Id of the <see cref="SlackEvent"/></param>
    /// <param name="eventType">The type of event.</param>
    /// <param name="performContext">Information about the Hangfire job.</param>
    /// <param name="integrationId"></param>
    /// <param name="cancellationToken">One of your standard cancellation tokens.</param>
    /// <exception cref="InvalidOperationException"></exception>
    [Queue(HangfireQueueNames.HighPriority)]
    [DisplayName("Process Slack Event {1}")]
    public async Task ProcessEventAsync(
        int id,
        string? eventType,
        PerformContext? performContext,
        int? integrationId,
        CancellationToken cancellationToken)
    {
        var jobId = (performContext?.BackgroundJob.Id).Require();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var slackEvent = await db.SlackEvents.FindAsync(new object[] { id }, cancellationToken).Require();

        cancellationToken.ThrowIfCancellationRequested();

        slackEvent.JobId = jobId;
        try
        {
            var eventEnvelope = !slackEvent.Content.Empty
                ? JsonConvert.DeserializeObject<IEventEnvelope<EventBody>>(slackEvent.Content.Reveal())
                : null;

            if (eventEnvelope is null)
            {
                slackEvent.Error = "Could not deserialize the event envelope.";
            }
            else
            {
                await _slackAdapter.ProcessEventAsync(eventEnvelope, _bot, integrationId, cancellationToken);
                slackEvent.Completed = DateTime.UtcNow;
            }
#pragma warning disable CA2016
            // We want this to save if possible even if we're canceling the operation, hence we don't pass the cancellation token.
            // ReSharper disable once MethodSupportsCancellation
            await db.SaveChangesAsync();
#pragma warning restore CA2016
        }
        catch (OperationCanceledException canceledException)
        {
            Log.EventProcessingCancelled(canceledException, slackEvent.EventId, eventType, id);
            // This is thrown if the operation is cancelled. We want to let this propagate so that Hangfire retries it.
            throw;
        }
        catch (Exception e) when (e.FindInnerException<TimeoutException>() is { } te)
        {
            Log.TimeoutExceptionDuringProcessing(te, slackEvent.EventId, eventType, id);
            // This is thrown if the operation times out. We want to let this propagate so that Hangfire retries it.
            throw;
        }
        catch (Exception e)
        {
            Log.ExceptionDuringProcessing(e, slackEvent.EventId, eventType, id);
        }
    }
}

public static partial class SlackEventProcessorLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Slack event {SlackEventId} (Id: {SlackEventDbId}, Type: {EventType}) processing cancelled")]
    public static partial void EventProcessingCancelled(
        this ILogger<SlackEventProcessor> logger,
        OperationCanceledException exception,
        string slackEventId,
        string? eventType,
        int slackEventDbId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Slack event {SlackEventId} (Id: {SlackEventDbId}, Type: {EventType}) timeout occurred.")]
    public static partial void TimeoutExceptionDuringProcessing(
        this ILogger<SlackEventProcessor> logger,
        TimeoutException te,
        string slackEventId,
        string? eventType,
        int slackEventDbId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Slack event {SlackEventId} (Id: {SlackEventDbId}, Type: {EventType}) exception occurred, no retries.")]
    public static partial void ExceptionDuringProcessing(
        this ILogger<SlackEventProcessor> logger,
        Exception e,
        string slackEventId,
        string? eventType,
        int slackEventDbId);
}
