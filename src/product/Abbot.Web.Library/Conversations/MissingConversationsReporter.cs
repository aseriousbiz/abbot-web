using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Threading;
using Humanizer;
using Microsoft.Extensions.Logging;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Slack;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.Conversations;

/// <summary>
/// Service used to report on conversations that should be tracked, but are not, for each <see cref="Room"/>
/// of an organization.
/// </summary>
public interface IMissingConversationsReporter
{
    /// <summary>
    /// Queries the Slack API for each <see cref="Room"/> in the <paramref name="organization"/> to see if there are
    /// any missing conversations that should be conversations. It then logs the missing conversations.
    /// </summary>
    /// <param name="organization">The organization to check for missing conversations.</param>
    /// <param name="cancellationToken">The cancellationToken.</param>
    Task LogUntrackedConversationsAsync(Organization organization, CancellationToken cancellationToken = default);
}

public class MissingConversationsReporter : IMissingConversationsReporter
{
    readonly Histogram<int> _missingConversationMetrics;

    readonly IConversationRepository _conversationRepository;
    readonly IConversationsApiClient _conversationsApiClient;
    readonly ISlackResolver _slackResolver;
    readonly IUserRepository _userRepository;
    readonly ISettingsManager _settingsManager;
    readonly ILogger<MissingConversationsReporter> _log;
    readonly IClock _clock;
    readonly IStopwatchFactory _stopwatchFactory;

    public MissingConversationsReporter(
        IConversationRepository conversationRepository,
        IConversationsApiClient conversationsApiClient,
        ISlackResolver slackResolver,
        IUserRepository userRepository,
        ISettingsManager settingsManager,
        ILogger<MissingConversationsReporter> log,
        IClock clock,
        IStopwatchFactory stopwatchFactory)
    {
        _conversationRepository = conversationRepository;
        _conversationsApiClient = conversationsApiClient;
        _slackResolver = slackResolver;
        _userRepository = userRepository;
        _settingsManager = settingsManager;
        _log = log;
        _clock = clock;
        _stopwatchFactory = stopwatchFactory;

        _missingConversationMetrics =
            AbbotTelemetry.Meter.CreateHistogram<int>("conversations.missing", "conversations");
    }

    public async Task LogUntrackedConversationsAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = _stopwatchFactory.StartNew();

        var rooms = organization
            .Rooms
            .Require()
            .Where(r => r.ManagedConversationsEnabled)
            .Where(r => r.IsActive())
            .ToList();

        var organizationName = organization.Name ?? "(unknown)";

        // At some point, we may want to limit the number of rooms per run to 50. The rate limit for
        // conversations.history https://api.slack.com/docs/rate-limits is 50 per minute.
        // But if we did that, we'd have to add a `DateTime` column to `Room` that lets us know when we last
        // checked the room so we can prioritize rooms based on how long ago they've been checked.
        var results = new Dictionary<string, IReadOnlyList<Result>>();

        foreach (var room in rooms)
        {
            try
            {
                var roomResults = await GetUntrackedConversationsAsync(room, cancellationToken);
                if (roomResults.Any())
                {
                    results[room.PlatformRoomId] = roomResults;
                }
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.TooManyRequests)
            {
                // Look at the Retry-After https://api.slack.com/docs/rate-limits and schedule the method for that time.
                // If there's no value, then let the next scheduled run handle it.
                var retryAfter = ex.Headers.RetryAfter?.Delta;

                var (retryDelay, retryValueHumanized) = retryAfter.HasValue
                    ? (retryAfter.Value, retryAfter.Value.Humanize())
                    : (TimeSpan.FromMinutes(1), "None: Using 1 minute");

                _log.RateLimitExceeded(
                    room.PlatformRoomId,
                    organizationName,
                    room.Organization.PlatformId,
                    retryAfter: retryValueHumanized);

                // We'll wait for the specified retry amount, and then move on to the next room.
                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (ApiException apiException)
            {
                _log.ApiExceptionCallingApi(
                    apiException,
                    room.PlatformRoomId,
                    organizationName,
                    room.Organization.PlatformId,
                    apiException.Content ?? string.Empty);
            }
            catch (OperationCanceledException canceledException)
            {
                LogCancelledResults(results, rooms.Count, canceledException, organization, stopwatch);
                throw;
            }
            catch (Exception exception)
            {
                _log.ExceptionCallingApi(
                    exception,
                    room.PlatformRoomId,
                    organizationName,
                    room.Organization.PlatformId);
            }
        }

        var elapsedHumanized = stopwatch.Elapsed.Humanize();
        if (results.Any())
        {
            LogResults(results, organization, stopwatch);
        }
        else
        {
            _log.Elapsed(
                organizationName,
                organization.PlatformId,
                elapsedHumanized);
        }
    }

    public record Summary(int MissingCount, string MissingMessageIdSummary);

    static Summary GatherResultsSummary(Dictionary<string, IReadOnlyList<Result>> results)
    {
        var missingCount = results.Sum(r => r.Value.Count);

        string Joiner(IEnumerable<Result> theResults)
        {
            var selection = theResults.ToList();
            return selection.Any()
                ? string.Join(", ", selection.Select(r => r.MessageId))
                : "None";
        }

        var missingMessageIds = results.Aggregate(
            string.Empty,
            (current, channelGroup) => current + $"""
    channel: {channelGroup.Key}, Missed: {Joiner(channelGroup.Value)}

""");

        return new Summary(missingCount, missingMessageIds);
    }

    void LogResults(Dictionary<string, IReadOnlyList<Result>> results, Organization organization, IStopwatch stopwatch)
    {
        var elapsedHumanized = stopwatch.Elapsed.Humanize();
        var (missingCount, missingMessageIds) = GatherResultsSummary(results);

        _missingConversationMetrics.Record(
            missingCount,
            AbbotTelemetry.CreateOrganizationTags(organization));

        _log.MessagesNotTracked(
            missingCount,
            organization.Name ?? "(unknown)",
            organization.PlatformId,
            elapsedHumanized,
            missingMessageIds);
    }

    void LogCancelledResults(
        Dictionary<string, IReadOnlyList<Result>> results,
        int roomCount,
        OperationCanceledException exception,
        Organization organization,
        IStopwatch stopwatch)
    {
        var elapsedHumanized = stopwatch.Elapsed.Humanize();
        var (missingCount, missingMessageIds) = GatherResultsSummary(results);

        _log.OperationCancelled(
            exception,
            results.Count,
            roomCount,
            missingCount,
            organization.Name ?? "(unknown)",
            organization.PlatformId,
            elapsedHumanized,
            missingMessageIds);
    }

    async Task<IReadOnlyList<Result>> GetUntrackedConversationsAsync(Room room, CancellationToken cancellationToken)
    {
        var channel = room.PlatformRoomId;
        var organizationName = room.Organization.Name ?? "(unknown)";
        var platformId = room.Organization.PlatformId;
        var (abbot, _) = await _userRepository.EnsureAbbotMemberAsync(room.Organization, cancellationToken);

        // Grab the last examined timestamp for this room, if none, just use a manufactured timestamp using
        // today as the date. It'll miss untracked conversations in the past, but moving forward will do the
        // right thing.
        var latestStoredTimestamp = await _settingsManager.GetLastVerifiedMessageIdAsync(room);
        var currentTimestamp = new SlackTimestamp(_clock.UtcNow).ToString();
        if (latestStoredTimestamp is null)
        {
            await _settingsManager.SetLastVerifiedMessageIdAsync(room, currentTimestamp, abbot);
        }

        var latestTimestamp = latestStoredTimestamp ?? currentTimestamp;
        if (!room.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            _log.OrganizationHasNoSlackApiToken();
            return Array.Empty<Result>();
        }

        // Grab all messages from the Slack API (up to 100) since we last checked.
        var response = await _conversationsApiClient.GetConversationHistoryAsync(
            apiToken,
            channel,
            oldest: latestTimestamp,
            inclusive: false,
            includeAllMetadata: true,
            cancellationToken: cancellationToken);

        if (response is not { Ok: true })
        {
            _log.FailedToCallConversationHistoryApi(
                error: response.ToString(),
                channel,
                organizationName,
                platformId,
                latestStoredTimestamp,
                currentTimestamp);

            return Array.Empty<Result>();
        }

        List<Result> results = new();
        foreach (var message in response.Body.Where(m => !m.IsDeleted()))
        {
            var isSupportee = await IsSupporteeAsync(message, room.Organization, room);
            if (isSupportee)
            {
                var messageId = message.Timestamp.Require();
                var conversation = await _conversationRepository.GetConversationByThreadIdAsync(
                    messageId,
                    room,
                    followHubThread: false,
                    cancellationToken);

                if (conversation is null && message.User is not null)
                {
                    var startedBy = await _slackResolver.ResolveMemberAsync(
                        message.User,
                        room.Organization,
                        forceRefresh: false);

                    if (startedBy is not null)
                    {
                        results.Add(new Result(messageId));
                    }
                }
            }
        }

        // The API returns messages ordered from most recent to oldest, so we need to grab the first message.
#pragma warning disable CA1826
        var mostRecentTimestamp = response.Body.FirstOrDefault()?.Timestamp ?? latestTimestamp;
#pragma warning restore CA1826

        if (mostRecentTimestamp != latestTimestamp)
        {
            await _settingsManager.SetLastVerifiedMessageIdAsync(room, mostRecentTimestamp, abbot);
        }

        return results;
    }

    async Task<bool> IsSupporteeAsync(SlackMessage message, Organization organization, Room room)
    {
        if (message is { SubType.Length: > 0 } || message.IsInThread() || message.User is null)
        {
            return false;
        }

        var member = await _slackResolver.ResolveMemberAsync(message.User, organization, forceRefresh: false);
        return member is null // We have no idea who the user is, better to alert.
               || !member.User.IsBot && ConversationTracker.IsSupportee(member, room);
    }

    record Result(string MessageId);
}

public static partial class MissingConversationsReporterLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Found {MissingCount} missing conversations for {OrganizationName} ({PlatformId}) in {ElapsedHumanized}.\n{MessageIds}")]
    public static partial void MessagesNotTracked(
        this ILogger<MissingConversationsReporter> logger,
        int missingCount,
        string organizationName,
        string platformId,
        string elapsedHumanized,
        string messageIds);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message =
            "Checked {OrganizationName} ({PlatformId}) and found no missing conversations in {ElapsedHumanized}.")]
    public static partial void Elapsed(
        this ILogger<MissingConversationsReporter> logger,
        string organizationName,
        string platformId,
        string elapsedHumanized);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message =
            "API {Error} attempting to check for missing conversations in {Channel}. {OrganizationName} ({PlatformId}). Latest Stored: {LatestStoredTimestamp}, Current: {CurrentTimestamp}")]
    public static partial void FailedToCallConversationHistoryApi(
        this ILogger<MissingConversationsReporter> logger,
        string error,
        string channel,
        string organizationName,
        string platformId,
        string? latestStoredTimestamp,
        string currentTimestamp);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Exception calling API: {PlatformRoomId} in org {OrganizationName} ({PlatformId}):\n{Content}")]
    public static partial void ApiExceptionCallingApi(
        this ILogger<MissingConversationsReporter> logger,
        Exception apiException,
        string platformRoomId,
        string organizationName,
        string platformId,
        string content);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "Exception calling API: {PlatformRoomId} in org {OrganizationName} ({PlatformId})")]
    public static partial void ExceptionCallingApi(
        this ILogger<MissingConversationsReporter> logger,
        Exception apiException,
        string platformRoomId,
        string organizationName,
        string platformId);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Error,
        Message =
            "Rate Limit Exceeded for room {PlatformRoomId} in org {OrganizationName} ({PlatformId}). Retrying in {RetryAfter}.")]
    public static partial void RateLimitExceeded(
        this ILogger<MissingConversationsReporter> logger,
        string platformRoomId,
        string organizationName,
        string platformId,
        string retryAfter);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Error,
        Message = "Rate Limit Exceeded for room {PlatformRoomId} in org {OrganizationName} ({PlatformId}). No Retry.")]
    public static partial void RateLimitExceededNoRetry(
        this ILogger<MissingConversationsReporter> logger,
        string platformRoomId,
        string organizationName,
        string platformId);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Error,
        Message =
            "Missing Conversations Repair job cancelled after examining {ResultsCount} of {RoomCount} rooms. Results so far:\nFound {MissingCount} missing conversations for {OrganizationName} ({PlatformId}) in {ElapsedHumanized}.\n{MessageIds}")]
    public static partial void OperationCancelled(
        this ILogger<MissingConversationsReporter> logger,
        OperationCanceledException exception,
        int resultsCount,
        int roomCount,
        int missingCount,
        string organizationName,
        string platformId,
        string elapsedHumanized,
        string messageIds);
}
