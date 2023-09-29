using System;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Infrastructure;

// Informational messages start at 1
public static partial class LoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting Abbot Web")]
    public static partial void Startup(this ILogger logger);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Attempting to create recurring job {JobName}")]
    public static partial void AttemptToCreateRecurringJob(this ILogger logger, string jobName);

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Information,
        Message =
            "About to send welcome message (Recipient: {PlatformUserId}, PlatformId: {PlatformId}, Org Name: {OrganizationName})")]
    public static partial void AboutToSendWelcomeMessage(
        this ILogger logger,
        string platformUserId,
        string platformId,
        string? organizationName);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Information,
        Message = "About to update team info (Organization Id: {OrganizationId})")]
    public static partial void AboutToUpdateOrganization(this ILogger logger, int organizationId);

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message =
            "Signal has {SubscriberCount} subscribers (Name: {SignalName}, Source Skill:(Id: {SourceSkillId}, Name: {SourceSkillName}), PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void SignalSubscriberCount(
        this ILogger logger,
        int subscriberCount,
        string signalName,
        int sourceSkillId,
        string sourceSkillName,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 40,
        Level = LogLevel.Information,
        Message =
            "Did not find skill assembly in memory cache, will attempt to download it. (Skill: (Id: {SkillId}, Name: {SkillName}), CacheKey: {CacheKey}, PlatformId: {PlatformId})")]
    public static partial void AssemblyNotFoundInCache(
        this ILogger logger,
        int skillId,
        string skillName,
        string cacheKey,
        string platformId);

    [LoggerMessage(
        EventId = 41,
        Level = LogLevel.Information,
        Message = "Organization is disabled.")]
    public static partial void OrganizationDisabled(this ILogger logger);
}
