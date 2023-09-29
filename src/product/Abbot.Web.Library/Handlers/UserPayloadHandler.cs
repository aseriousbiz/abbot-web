using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Handles incoming user events.
/// </summary>
public class UserPayloadHandler : IPayloadHandler<UserEventPayload>
{
    static readonly ILogger<UserPayloadHandler> Log = ApplicationLoggerFactory.CreateLogger<UserPayloadHandler>();

    readonly IOrganizationRepository _organizationRepository;
    readonly IUserRepository _userRepository;
    readonly IOptions<AbbotOptions> _abbotOptions;
    readonly IClock _clock;

    static readonly Counter<long> UserChangeCount = AbbotTelemetry.Meter.CreateCounter<long>(
        "users.change.count",
        "changes",
        "Counts changes to users in the system.");
    static readonly Histogram<long> UserChangeDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
        "users.change.duration",
        "milliseconds",
        "The time it takes to process a user change.");
    static readonly Histogram<long> UserChangeLatency = AbbotTelemetry.Meter.CreateHistogram<long>(
        "users.change.latency",
        "milliseconds",
        "The time between when a user change event is received and when it is processed.");

    public UserPayloadHandler(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IOptions<AbbotOptions> abbotOptions,
        IClock clock)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _abbotOptions = abbotOptions;
        _clock = clock;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<UserEventPayload> platformEvent)
    {
        if (_abbotOptions.Value.IgnoreUserChangeEvents)
        {
            return;
        }

        var messageOrganization = platformEvent.Organization;
        var userEventPayload = platformEvent.Payload;
        var metricTags = new TagList()
        {
            { "organization_plan", messageOrganization.PlanType.ToString() },
            { "organization_trial_plan", messageOrganization.Trial?.Plan.ToString() },
            { "organization_effective_plan", messageOrganization.GetPlan().Type.ToString() },
            { "organization_enabled", messageOrganization.Enabled }
        };

        var latency = _clock.UtcNow - platformEvent.Timestamp.UtcDateTime;
        UserChangeLatency.Record((long)latency.TotalMilliseconds, metricTags);

        if (!ShouldProcessChange(messageOrganization))
        {
            metricTags.SetSkipped();
            UserChangeCount.Add(1, metricTags);
            return;
        }

        Log.ReceivedUserEvent(
            userEventPayload.IsBot,
            userEventPayload.Deleted,
            messageOrganization.PlatformId,
            userEventPayload.Id,
            userEventPayload.TimeZoneId);

        if (userEventPayload.IsBot)
        {
            return;
        }

        var userOrganization = userEventPayload.PlatformId is null || userEventPayload.PlatformId == messageOrganization.PlatformId
            ? messageOrganization
            : await _organizationRepository.GetAsync(userEventPayload.PlatformId);

        if (userOrganization is null)
        {
            // For now at least, we don't create organizations "foreign" organizations unless a user from that org sends a message.
            return;
        }

        // Don't time skipped events
        using var _ = UserChangeDuration.Time(metricTags);

        try
        {
            await _userRepository.EnsureAndUpdateMemberAsync(userEventPayload, userOrganization);
            metricTags.SetSuccess();
        }
        catch (Exception e)
        {
            metricTags.SetFailure(e);
            Log.ExceptionEnsuringMember(
                e,
                userEventPayload.IsBot,
                userEventPayload.Deleted,
                messageOrganization.PlatformId,
                userEventPayload.RealName,
                userEventPayload.DisplayName,
                userEventPayload.Id);
        }
        finally
        {
            UserChangeCount.Add(1, metricTags);
        }
    }

    static bool ShouldProcessChange(Organization messageOrganization)
    {
        if (!messageOrganization.Enabled)
        {
            return false;
        }

        var plan = messageOrganization.GetPlan();
        if (plan.Type is PlanType.Free or PlanType.None)
        {
            return false;
        }

        return true;
    }
}

public static partial class UserPayloadHandlerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "User event received (IsBot: {IsBot}, IsDeleted: {IsDeleted}, PlatformUserId: {PlatformUserId}, PlatformId: {PlatformId}, TimeZoneId: {TimeZoneId})")]
    public static partial void ReceivedUserEvent(
        this ILogger<UserPayloadHandler> logger,
        bool isBot,
        bool isDeleted,
        string platformId,
        string platformUserId,
        string? timeZoneId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message =
            "Exception ensuring member {IsBot} {IsDeleted} {PlatformId} {RealName} {DisplayName} {PlatformUserId}")]
    public static partial void ExceptionEnsuringMember(
        this ILogger<UserPayloadHandler> logger,
        Exception exception,
        bool isBot,
        bool isDeleted,
        string platformId,
        string realName,
        string? displayName,
        string platformUserId);
}
