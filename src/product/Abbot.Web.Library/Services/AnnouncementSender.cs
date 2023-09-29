using System.Linq;
using Hangfire;
using Hangfire.Server;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Extensions;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Services;

public class AnnouncementSender
{
    static readonly ILogger<AnnouncementSender> Log = ApplicationLoggerFactory.CreateLogger<AnnouncementSender>();

    readonly IAnnouncementsRepository _announcementsRepository;
    readonly IAnnouncementScheduler _announcementScheduler;
    readonly AnnouncementDispatcher _announcementDispatcher;
    readonly IPublishEndpoint _publishEndpoint;
    readonly ISlackApiClient _slackApiClient;
    readonly IBackgroundJobClient _backgroundJobClient;
    readonly IClock _clock;

    public AnnouncementSender(
        IAnnouncementsRepository announcementsRepository,
        IAnnouncementScheduler announcementScheduler,
        AnnouncementDispatcher announcementDispatcher,
        IPublishEndpoint publishEndpoint,
        ISlackApiClient slackApiClient,
        IBackgroundJobClient backgroundJobClient,
        IClock clock)
    {
        _announcementsRepository = announcementsRepository;
        _announcementScheduler = announcementScheduler;
        _announcementDispatcher = announcementDispatcher;
        _publishEndpoint = publishEndpoint;
        _slackApiClient = slackApiClient;
        _backgroundJobClient = backgroundJobClient;
        _clock = clock;
    }

    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task SendReminderAsync(int announcementId)
    {
        var announcement = await _announcementsRepository.RequireForSchedulingAsync(announcementId);
        Expect.NotNull(announcement.ScheduledDateUtc);

        if (announcement.DateStartedUtc is { } dateStartedUtc)
        {
            Log.AnnouncementAlreadyStarted(announcement.Id, announcement.ScheduledDateUtc, dateStartedUtc);
            // It's already been started.
            return;
        }

        // Send Reminder and schedule broadcast.
        var messageUrl = announcement.GetMessageUrl();
        var hyperlink = new Hyperlink(messageUrl, "This message");

        var fallbackText = $":mega: {messageUrl} will be posted in *1 hour*.";
        await _slackApiClient.SendDirectMessageAsync(
            announcement.Organization,
            announcement.Creator,
            fallbackText,
            new Section(new MrkdwnText($":mega: {hyperlink} will be posted in {announcement.Messages.ToRoomMentionList()} in *1 hour*.")));

        await _announcementScheduler.ScheduleAnnouncementBroadcastAsync(
            announcement,
            announcement.Creator,
            scheduleReminder: false /* Since we're in the reminder interval, make sure we just the broadcast message. */);
    }

    public async Task BroadcastAnnouncementAsync(int announcementId)
    {
        var announcement = await _announcementsRepository.RequireForSchedulingAsync(announcementId);
        if (announcement.DateStartedUtc is not null)
        {
            Log.AnnouncementAlreadyStarted(announcement.Id, announcement.ScheduledDateUtc, announcement.DateStartedUtc);
            // It's already been started.
            return;
        }

        var announcementTarget = _announcementDispatcher.GetAnnouncementTarget(announcement);
        var messages = await announcementTarget.ResolveAnnouncementRoomsAsync(announcement);
        if (!announcement.Messages.Any())
        {
            announcement.Messages.AddRange(messages);
        }

        // We wait till the last minute to grab the announcement text.
        var announcementText = await GetAnnouncementTextAsync(announcement);

        await _announcementsRepository.UpdateAnnouncementTextAndStartDateAsync(
            announcement,
            announcementText,
            _clock.UtcNow);

        foreach (var message in announcement.Messages)
        {
            _backgroundJobClient.Enqueue(() => SendAnnouncementMessageAsync(message.Id, null! /* Hangfire will fill this in */));
        }
    }

    /// <summary>
    /// This will look at all messages that have been sent and attempt to update the announcement.
    /// </summary>
    /// <param name="announcementId">The Id of the announcement.</param>
    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task UpdateSentAnnouncementMessagesAsync(int announcementId)
    {
        var announcement = await _announcementsRepository.RequireForSchedulingAsync(announcementId);
        if (announcement.DateStartedUtc is null)
        {
            // It hasn't started yet. No need to update.
            return;
        }

        // We wait till the last minute to grab the announcement text.
        var announcementText = await GetAnnouncementTextAsync(announcement);

        await _announcementsRepository.UpdateAnnouncementTextAndStartDateAsync(
            announcement,
            announcementText,
            _clock.UtcNow);

        foreach (var message in announcement.Messages)
        {
            _backgroundJobClient.Enqueue(() => UpdateAnnouncementMessageAsync(message.Id, null! /* Hangfire will fill this in */));
        }
    }

    async Task<string> GetAnnouncementTextAsync(Announcement announcement)
    {
        var message = await _slackApiClient.Conversations.GetMessageAsync(announcement);

        if (message is not { Text.Length: > 0 })
        {
            // TODO: If we can't get the announcement text, we should audit log it, rather than throw an exception.

            throw new InvalidOperationException(
                $"The announcement {announcement.Id} could not be sent because the message {announcement.SourceMessageId} in room {announcement.SourceRoom.PlatformRoomId} could not be found or has empty text.");
        }

        return message.Text;
    }

    [AutomaticRetry(Attempts = 5)]
    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task SendAnnouncementMessageAsync(int announcementMessageId, PerformContext? performContext)
    {
        var announcementMessage = await _announcementsRepository.RequireMessageForSendingAsync(announcementMessageId);
        if (!announcementMessage.Announcement.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            throw new InvalidOperationException($"The organization {announcementMessage.Announcement.Organization.Id} has no API token.");
        }
        var announcement = announcementMessage.Announcement;

        var org = announcement.Organization;
        var (userName, iconUrl) = announcement.SendAsBot
            ? (org.BotName, org.BotResponseAvatar ?? org.BotAvatar)
            : (announcement.Creator.DisplayName, announcement.Creator.Avatar);

        var messageRequest = new MessageRequest
        {
            Text = announcement.Text,
            Channel = announcementMessage.Room.PlatformRoomId,
            UserName = userName,
            IconUrl = iconUrl is { Length: > 0 } avatar ? new Uri(avatar) : null,
        };
        var response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, messageRequest);

        // If this fails, we can throw an exception and let Hangfire retry the job.
        var (timestamp, error) = response.Ok
            ? (response.Body.Timestamp, (string?)null)
            : ((string?)null, response.Error);

        if (error is "request_timeout" or "service_unavailable")
        {
            var retryCount = performContext?.GetJobParameter<int>("RetryCount");

            Log.RetryAttempt(retryCount.GetValueOrDefault());

            if (retryCount < 5)
            {
                throw new InvalidOperationException($"Slack returned an `{error}` message ({response}).");
            }
        }

        await _announcementsRepository.UpdateMessageSendCompletedAsync(
            announcementMessage,
            timestamp,
            error,
            _clock.UtcNow);

        await _publishEndpoint.Publish(new AnnouncementMessageCompleted(announcement));
    }

    [AutomaticRetry(Attempts = 5)]
    [Queue(HangfireQueueNames.NormalPriority)]
    public async Task UpdateAnnouncementMessageAsync(int announcementMessageId, PerformContext? performContext)
    {
        var announcementMessage = await _announcementsRepository.RequireMessageForSendingAsync(announcementMessageId);
        if (!announcementMessage.Announcement.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            throw new InvalidOperationException($"The organization {announcementMessage.Announcement.Organization.Id} has no API token.");
        }
        var announcement = announcementMessage.Announcement;

        if (announcementMessage.MessageId is not { Length: > 0 } messageId)
        {
            return;
        }

        var messageRequest = new MessageRequest
        {
            Timestamp = messageId,
            Text = announcement.Text,
            Channel = announcementMessage.Room.PlatformRoomId,
            UserName = announcement.Creator.DisplayName,
            IconUrl = announcement.Creator.Avatar is { Length: > 0 } avatar ? new Uri(avatar) : null,
        };
        var response = await _slackApiClient.UpdateMessageAsync(apiToken, messageRequest);

        // If this fails, we can throw an exception and let Hangfire retry the job.
        var (timestamp, error) = response.Ok
            ? (response.Timestamp, (string?)null)
            : ((string?)null, response.Error);

        if (error is "request_timeout" or "service_unavailable")
        {
            var retryCount = performContext?.GetJobParameter<int>("RetryCount");

            Log.RetryAttempt(retryCount.GetValueOrDefault());

            if (retryCount < 5)
            {
                throw new InvalidOperationException($"Slack returned an `{error}` message.");
            }
        }

        await _announcementsRepository.UpdateMessageSendCompletedAsync(
            announcementMessage,
            timestamp,
            error,
            _clock.UtcNow);

        await _publishEndpoint.Publish(new AnnouncementMessageCompleted(announcement));
    }
}

public static partial class AnnouncementSenderLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message =
            "Announcement ({AnnouncementId} scheduled to start at {ScheduledStartDate} already started at {StartDate}.")]
    public static partial void AnnouncementAlreadyStarted(
        this ILogger<AnnouncementSender> logger,
        int announcementId,
        DateTime? scheduledStartDate,
        DateTime? startDate);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Retry Attempt {RetryAttempt}.")]
    public static partial void RetryAttempt(this ILogger<AnnouncementSender> logger, int retryAttempt);
}
