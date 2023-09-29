using System;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Infrastructure;

// Warning messages start at 3000
public static partial class LoggingExtensions
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Warning,
        Message =
            "Staff user accessing audit event (Identifier: {Identifier}, Reason: {Reason}, Staff User {UserName}, Id: {UserId})")]
    public static partial void StaffUserAccess(
        this ILogger logger,
        Guid identifier,
        string reason,
        string userName,
        int userId);

    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Warning,
        Message =
            "Staff user accessed slack event content (EventId: {SlackEventId}, Reason: {Reason}, Staff User {UserName}, Id: {UserId})")]
    public static partial void StaffSlackEventAccess(
        this ILogger logger,
        string slackEventId,
        string reason,
        string userName,
        int userId);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Azure App Configuration key not found. Run script/bootstrap to set up the secrets.")]
    public static partial void AzureAppConfigurationKeyNotFound(this ILogger logger);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "UploadAsync returned a null response value (Folder: {UploadFolder}, Image: {AvatarImage})")]
    public static partial void UploadFailed(this ILogger logger, string uploadFolder, string avatarImage);

    [LoggerMessage(
        EventId = 3040,
        Level = LogLevel.Warning,
        Message = "Attempted to send a direct message on an unsupported platform.")]
    public static partial void ExceptionSendingDirectMessage(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3050,
        Level = LogLevel.Warning,
        Message = "Could not load assembly (AssemblyPath: {AssemblyPath}, DocumentationPath: {DocumentationPath})")]
    public static partial void ExceptionLoadingAssembly(
        this ILogger logger,
        Exception exception,
        string assemblyPath,
        string documentationPath);

    [LoggerMessage(
        EventId = 3070,
        Level = LogLevel.Warning,
        Message =
            "Could not retrieve Access Token for user (NameIdentifier: {NameIdentifier}, PlatformUserId: {PlatformUserId}, PlatformType: {PlatformType})")]
    public static partial void ErrorRetrievingAccessToken(
        this ILogger logger,
        string nameIdentifier,
        string platformUserId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 3071,
        Level = LogLevel.Warning,
        Message = "Invalid token header `" + CommonConstants.SkillApiTokenHeaderName +
                  "` (Values Count: {ValuesCount}, Skill Id: {SkillId}, User Id: {UserId}, Url: {Url})")]
    public static partial void InvalidApiTokenHeader(
        this ILogger logger,
        int valuesCount,
        int skillId,
        int userId,
        string url);

    [LoggerMessage(
        EventId = 3072,
        Level = LogLevel.Warning,
        Message = "Could not validate API Token (Skill Id: {SkillId}, User Id: {UserId}, Type: {Type})")]
    public static partial void InvalidApiToken(
        this ILogger logger,
        int skillId,
        int userId,
        Type type);
}
