using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Slack.Events;

namespace Serious.Abbot;

public static class GlobalLoggingScopes
{
    static readonly Func<ILogger, string, string, string, string, string?, IDisposable?> RequestScope =
        LoggerMessage.DefineScope<string, string, string, string, string?>(
            "Request: {RequestMethod} {RequestHost} {RequestPath} {RequestQuery} {RequestTurboFrame}");

    static readonly Func<ILogger, int, ConversationState, string?, IDisposable?> ConversationScope =
        LoggerMessage.DefineScope<int, ConversationState, string?>(
            "Conversation: {ConversationId}, {ConversationState}, {HubThreadId}");

    static readonly Func<ILogger, string, string?, IDisposable?> ConversationEventScope =
        LoggerMessage.DefineScope<string, string?>(
            "Event: {ConversationEventName} (ThreadId: {ConversationEventThreadId})");

    static readonly Func<ILogger, int, string, string, IDisposable?> OrganizationScope =
        LoggerMessage.DefineScope<int, string, string>(
            "Organization: {OrganizationId}, {OrganizationPlatformId}, {OrganizationSlug}");

    static readonly Func<ILogger, int, string?, IDisposable?> MemberScope =
        LoggerMessage.DefineScope<int, string?>(
            "Member: {MemberId}, {MemberPlatformId}");

    static readonly Func<ILogger, Id<Playbook>, string, IDisposable?> PlaybookScope =
        LoggerMessage.DefineScope<Id<Playbook>, string>(
            "Playbook: {PlaybookId}, {PlaybookSlug}");

    static readonly Func<ILogger, Id<PlaybookVersion>, int, IDisposable?> PlaybookVersionScope =
        LoggerMessage.DefineScope<Id<PlaybookVersion>, int>(
            "Playbook Version: {PlaybookVersionId} {VersionNumber}");

    static readonly Func<ILogger, Guid, int, string?, IDisposable?> PlaybookRunScope =
        LoggerMessage.DefineScope<Guid, int, string?>(
            "Run: {PlaybookRunId} (Version {PlaybookVersion}), State: {PlaybookRunState}");

    static readonly Func<ILogger, Guid, int, IDisposable?> PlaybookRunGroupScope =
        LoggerMessage.DefineScope<Guid, int>(
            "Run Group: {PlaybookRunGroupId} (Version {PlaybookVersion})");

    static readonly Func<ILogger, int, string, IDisposable?> SkillScope =
        LoggerMessage.DefineScope<int, string>(
            "Skill: {SkillId}, {SkillName}");

    static readonly Func<ILogger, int, string, IDisposable?> RoomScope =
        LoggerMessage.DefineScope<int, string>(
            "Room: {RoomId}, {RoomPlatformId}");

    static readonly Func<ILogger, int, int?, string?, IDisposable?> HubScope =
        LoggerMessage.DefineScope<int, int?, string?>(
            "Hub: {HubId}, {HubRoomId}, {HubRoomPlatformId}");

    /// <summary>
    /// Enters all relevant scopes for the given <see cref="HttpContext" />
    /// </summary>
    /// <param name="logger">The logger to apply scopes to.</param>
    /// <param name="context">The <see cref="HttpContext"/> in which the current action is taking place.</param>
    public static IDisposable? BeginRequestScope(
        this ILogger logger,
        HttpContext context)
    {
        // Check if this is a Turbo request
        var turboFrame = context.Request.Headers.TryGetValue("turbo-frame", out var frame)
            ? frame.ToString()
            : null;

        // It may be tempting to put client information (IP and such) here, but remember that's identifying information!
        return RequestScope(
            logger,
            context.Request.Method,
            context.Request.Host.ToString(),
            context.Request.Path.ToString(),
            context.Request.QueryString.ToString(),
            turboFrame);
    }

    /// <summary>
    /// Enters all relevant scopes for the given <see cref="Room"/> and <see cref="Hub"/> scopes.
    /// </summary>
    /// <param name="logger">The logger to apply scopes to.</param>
    /// <param name="room">The <see cref="Room"/> context in which the current action is taking place.</param>
    public static IDisposable? BeginRoomAndHubScopes(
        this ILogger logger,
        Room? room)
    {
        return Disposable.Combine(
            BeginRoomScope(logger, room),
            BeginHubScope(logger, room?.Hub));
    }

    /// <summary>
    /// Enters all relevant scopes for the given <see cref="Conversation"/>, including the <see cref="Hub"/> and <see cref="Room"/> scopes.
    /// </summary>
    /// <param name="logger">The logger to apply scopes to.</param>
    /// <param name="conversation">The <see cref="Conversation"/> context in which the current action is taking place.</param>
    /// <returns></returns>
    public static IDisposable? BeginConversationRoomAndHubScopes(
        this ILogger logger,
        Conversation? conversation)
    {
        return Disposable.Combine(
            BeginRoomScope(logger, conversation?.Room),
            BeginHubScope(logger, conversation?.Hub),
            BeginConversationScope(logger, conversation));
    }

    // Private because we believe BeginConversationRoomAndHubScopes can be used everywhere.
    static IDisposable? BeginConversationScope(
        this ILogger logger,
        Conversation? conversation)
        => conversation is null
            ? null
            : ConversationScope(
                logger,
                conversation.Id,
                conversation.State,
                conversation.HubThreadId);

    public static IDisposable? BeginConversationEventScopes(
        this ILogger logger,
        ConversationEvent conversationEvent)
    {
        return Disposable.Combine(
            BeginConversationRoomAndHubScopes(logger, conversationEvent.Conversation),
            BeginMemberScope(logger, conversationEvent.Member),
            ConversationEventScope(logger, conversationEvent.GetType().Name, conversationEvent.ThreadId));
    }

    public static IDisposable? BeginOrganizationScope(
        this ILogger logger,
        Organization? organization)
    {
        return organization is null
            ? null
            : OrganizationScope(
                logger,
                organization.Id,
                organization.PlatformId,
                organization.Slug);
    }

    public static IDisposable? BeginMemberScope(
        this ILogger logger,
        Member? member)
    {
        return member is null ? null : MemberScope(
            logger,
            member.Id,
            member.User?.PlatformUserId);
    }

    public static IDisposable? BeginPlaybookScope(
        this ILogger logger,
        Playbook? playbook)
    {
        return playbook is null
            ? null
            : PlaybookScope(
                logger,
                playbook,
                playbook.Slug);
    }

    public static IDisposable? BeginPlaybookVersionScope(
        this ILogger logger,
        PlaybookVersion? playbookVersion)
    {
        return playbookVersion is null
            ? null
            : PlaybookVersionScope(
                logger,
                playbookVersion,
                playbookVersion.Version);
    }

    public static IDisposable? BeginPlaybookRunGroupScope(
        this ILogger logger,
        PlaybookRunGroup group)
    {
        return PlaybookRunGroupScope(
            logger,
            group.CorrelationId,
            group.Version);
    }

    public static IDisposable? BeginPlaybookRunScope(
        this ILogger logger,
        PlaybookRun run)
    {
        return PlaybookRunScope(
                logger,
                run.CorrelationId,
                run.Version,
                run.State);
    }

    public static IDisposable? BeginSkillScope(
        this ILogger logger,
        Skill? skill)
    {
        return skill is null
            ? null
            : SkillScope(
                logger,
                skill.Id,
                skill.Name);
    }

    public static IDisposable? BeginRoomScope(
        this ILogger logger,
        Room? room)
    {
        return room is null
            ? null
            : RoomScope(
                logger,
                room.Id,
                room.PlatformRoomId);
    }

    public static IDisposable? BeginHubScope(
        this ILogger logger,
        Hub? hub)
    {
        return hub is null
            ? null
            : HubScope(
                logger,
                hub.Id,
                hub.Room?.Id,
                hub.Room?.PlatformRoomId);
    }
}
