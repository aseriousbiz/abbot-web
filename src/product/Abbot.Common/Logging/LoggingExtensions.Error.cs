using System;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Infrastructure;

// This type contains "Universal" error message types as well as a number of legacy error messages.
// Sometimes we have log messages that are applicable to all sorts of log categories, and we put them here.
// Event Ids should always start with 4000 or 5000
// Note that methods that log Exceptions should start with "Exception". Those that log errors that are not
// embodied by an exception should start with "Error"
public static partial class LoggingExtensions
{

    // This event disables two warnings:
    // * SYSLIB1015 requires that all arguments be used in the message.
    //   Putting the event details JSON in the message is unhelpful, and just having it as a parameter still attaches it to the event
    // * InconsistentNaming: When you don't reference a parameter in the message, the parameter name is used as the event property name, so we want PascalCasing.
#pragma warning disable SYSLIB1015
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Error,
        Message = "[OnTurnError] unhandled error processing `{EnvelopeEventType}` envelope of type `{EventType}`: EventId: {SlackEventId}, Team: {Team}.")]
    // ReSharper disable once InconsistentNaming
    public static partial void ExceptionUnhandledOnTurnForSlack(
        this ILogger logger,
        Exception exception,
        string slackEventId,
        string envelopeEventType,
        string eventType,
        string team,
        // ReSharper disable once InconsistentNaming
        string EventDetails);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message =
            "[OnTurnError] unhandled error calling skill (Name: {SkillName}, Id: {SkillId}, Endpoint: {Endpoint}) from slack event ({SlackEventType} event: EventId: {SlackEventId}, Team: {Team})")]
    public static partial void ExceptionCallingSkillFromSlack(
        this ILogger logger,
        Exception exception,
        int skillId,
        string skillName,
        string slackEventId,
        string slackEventType,
        string team,
        Uri? endpoint,
        // ReSharper disable once InconsistentNaming
        string EventDetails);
#pragma warning restore SYSLIB1015

    [LoggerMessage(
        EventId = 4012,
        Level = LogLevel.Error,
        Message =
            "Exception calling skill (Name: {SkillName}, Id: {SkillId}, PlatformId: {PlatformId}, Endpoint: {EndPoint})")]
    public static partial void ExceptionCallingSkill(
        this ILogger logger,
        Exception exception,
        string skillName,
        int skillId,
        string platformId,
        Uri? endpoint);

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Error,
        Message = "[OnTurnError] unhandled error processing message")]
    public static partial void ExceptionUnhandledOnTurn(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Error,
        Message = "Compiler crashed: (CacheKey: {CacheKey}, Code: {Code})")]
    public static partial void ExceptionCompilingCode(this ILogger logger, Exception exception, string cacheKey,
        string code);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Error,
        Message =
            "Exception thrown emitting assembly streams for skill (Name: `{SkillName}`, CacheKey: {CacheKey}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void ExceptionEmittingAssemblyStreams(
        this ILogger logger,
        Exception exception,
        string skillName,
        string cacheKey,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 4006,
        Level = LogLevel.Error,
        Message =
            "Exception attempting to check if the assembly exists (AssemblyName: `{AssemblyName}`, CacheKey: {CacheKey}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void ExceptionCheckingAssembly(
        this ILogger logger,
        Exception exception,
        string assemblyName,
        string cacheKey,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 4007,
        Level = LogLevel.Error,
        Message =
            "Exception thrown while trying to download skill assembly (CacheKey: {CacheKey}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void ExceptionDownloadingSkillAssembly(
        this ILogger logger,
        Exception exception,
        string cacheKey,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 4008,
        Level = LogLevel.Error,
        Message =
            "Exception thrown while trying to download skill symbols (CacheKey: {CacheKey}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void ExceptionDownloadingSymbols(
        this ILogger logger,
        Exception exception,
        string cacheKey,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 4009,
        Level = LogLevel.Error,
        Message =
            "Exception thrown while trying to upload skill assembly (SkillName: {SkillName}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void ExceptionUploadingSkillAssembly(
        this ILogger logger,
        Exception exception,
        string skillName,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 4011,
        Level = LogLevel.Error,
        Message = "Exception creating share (ShareName: {ShareName}, ShareType: {ShareType})")]
    public static partial void ExceptionCreatingShare(
        this ILogger logger,
        Exception exception,
        string shareName,
        string shareType);

    [LoggerMessage(
        EventId = 4013,
        Level = LogLevel.Error,
        Message = "Error attempting to run pattern (Pattern: {Pattern}, Id: {PatternId}, SkillId: {SkillId}, PlatformId: {PlatformId})")]
    public static partial void ExceptionMatchingPattern(this ILogger logger,
        Exception exception,
        string pattern,
        int patternId,
        int skillId,
        string platformId);

    [LoggerMessage(
        EventId = 4014,
        Level = LogLevel.Error,
        Message = "Error retrieving brain data (Key: {Key}, Url: {Url})")]
    public static partial void ExceptionRetrievingBrainData(
        this ILogger logger,
        Exception exception,
        string key,
        Uri url);

    [LoggerMessage(
        EventId = 4015,
        Level = LogLevel.Error,
        Message =
            "Could not retrieve user's organization info (OrganizationId: {OrganizationId}, PlatformUserId: {PlatformUserId}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void ExceptionRetrievingOrganizationInfo(
        this ILogger logger,
        Exception exception,
        int organizationId,
        string platformUserId,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 4016,
        Level = LogLevel.Error,
        Message =
            "Could not retrieve user's organization info (OrganizationId: {OrganizationId}, PlatformId: {PlatformId}, PlatformType: {PlatformType}, Error: {ErrorMessage})")]
    public static partial void ErrorRetrievingOrganizationInfo(
        this ILogger logger,
        int organizationId,
        string platformId,
        PlatformType platformType,
        string errorMessage);

    [LoggerMessage(
        EventId = 4017,
        Level = LogLevel.Error,
        Message =
            "Could not get user list for organization (OrganizationId: {OrganizationId}, Name: {OrganizationName}, PlatformId: {PlatformId}, PlatformType: {PlatformType}, Error: {ErrorMessage})")]
    public static partial void ErrorRetrievingUsers(
        this ILogger logger,
        int organizationId,
        string? organizationName,
        string platformId,
        PlatformType platformType,
        string? errorMessage);

    [LoggerMessage(
        EventId = 4018,
        Level = LogLevel.Error,
        Message =
            "Error adding value to list (Name: {ListName}, Value: {ListValue}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void ExceptionAddingValueToList(
        this ILogger logger,
        Exception exception,
        string listName,
        string listValue,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 4019,
        Level = LogLevel.Error,
        Message =
            "Error removing value from list (Name: {ListName}, Value: {ListValue}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void ExceptionRemovingValueFromList(
        this ILogger logger,
        Exception exception,
        string listName,
        string listValue,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 4020,
        Level = LogLevel.Error,
        Message = "Entity not found by Id (Id: {EntityId}, Type: {EntityType})")]
    public static partial void EntityNotFound(this ILogger logger, object entityId, Type entityType);

    public static void EntityNotFound<T>(this ILogger logger, Id<T> entityId)
        where T : class =>
        logger.EntityNotFound(entityId.Value, typeof(T));

    [LoggerMessage(
        EventId = 4021,
        Level = LogLevel.Error,
        Message = "Organization not found (PlatformId: {PlatformId})")]
    public static partial void OrganizationNull(this ILogger logger, string platformId);

    [LoggerMessage(
        EventId = 4022,
        Level = LogLevel.Error,
        Message = "Access Token not found in Channel Data (PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void AccessTokenNotFound(this ILogger logger, string platformId, PlatformType platformType);

    [LoggerMessage(
        EventId = 4024,
        Level = LogLevel.Error,
        Message = "Setting is missing or incorrect (Setting: {SettingName}, Value: {SettingValue})")]
    public static partial void SettingMissingOrIncorrect(
        this ILogger logger,
        string settingName,
        string settingValue);

    [LoggerMessage(
        EventId = 4030,
        Level = LogLevel.Error,
        Message = "The response from SendGrid is null. It could mean the API Key is incorrect.")]
    public static partial void NullResponseFromSendGrid(this ILogger logger);

    [LoggerMessage(
        EventId = 4040,
        Level = LogLevel.Error,
        Message = "{Formatter} received null message options.")]
    public static partial void FormatterReceivedNullMessageOptions(
        this ILogger logger, string formatter);

    [LoggerMessage(
        EventId = 4059,
        Level = LogLevel.Error,
        Message = "Exception calling Slack API.")]
    public static partial void ExceptionCallingSlackApi(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 4060,
        Level = LogLevel.Error,
        Message = "Error calling Slack API: {Error}.")]
    public static partial void ErrorCallingSlackApi(this ILogger logger, string error);

    [LoggerMessage(
        EventId = 4061,
        Level = LogLevel.Error,
        Message = "Error deserializing incoming Slack message: {Error} {ChannelData}.")]
    public static partial void ErrorDeserializingSlackMessage(this ILogger logger, string error, object? channelData);

    [LoggerMessage(
        EventId = 4062,
        Level = LogLevel.Error,
        Message = "Organization has no Slack API token")]
    public static partial void OrganizationHasNoSlackApiToken(this ILogger logger);
}
