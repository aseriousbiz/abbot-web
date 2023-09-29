using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Infrastructure;

// Debug Messages start at 10,000
public static partial class LoggingExtensions
{
    [LoggerMessage(
        EventId = 10000,
        Level = LogLevel.Debug,
        Message = "Method entered ({Type}.{MethodName}), Extra Context: {ExtraContext}")]
    public static partial void MethodEntered(
        this ILogger logger,
        Type type,
        string methodName,
        object? extraContext);

    [LoggerMessage(
        EventId = 10011,
        Level = LogLevel.Trace,
        Message = "Method entered ({Type}.{MethodName}), Extra Context: {ExtraContext}")]
    public static partial void TraceMethodEntered(
        this ILogger logger,
        Type type,
        string methodName,
        object? extraContext);

    [LoggerMessage(
        EventId = 10001,
        Level = LogLevel.Debug,
        Message = "Method entered (Name: {Type}.{MethodName}, Skill: (Id: {SkillId}, Name: {SkillName}))")]
    public static partial void SkillMethodEntered(
        this ILogger logger,
        Type type,
        string methodName,
        int? skillId,
        string skillName);

    [LoggerMessage(
        EventId = 10009,
        Level = LogLevel.Debug,
        Message =
            "Sending Abbot CLI Request (Skill (Id: {SkillId}, Name: {SkillName}, Arguments: {Arguments}, CacheKey: {CacheKey}), PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void SendingAbbotCliRequest(
        this ILogger logger,
        int skillId,
        string skillName,
        string arguments,
        string cacheKey,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 10015,
        Level = LogLevel.Debug,
        Message =
            "UploadAsync succeeded (Folder: {UploadFolder}, Image: {AvatarImage}, Content Hash: {ContentHash})")]
    public static partial void UploadSucceeded(
        this ILogger logger,
        string uploadFolder,
        string avatarImage,
        byte[] contentHash);

    [LoggerMessage(
        EventId = 10016,
        Level = LogLevel.Debug,
        Message =
            "Created MessageContext (Skill: {SkillName}, Arguments: {Arguments}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void CreatedMessageContext(
        this ILogger logger,
        string skillName,
        string arguments,
        string platformId,
        PlatformType platformType);

    [LoggerMessage(
        EventId = 10021,
        Level = LogLevel.Debug,
        Message =
            "{ClaimType} Claim is null for current principal (Authenticated: {Authenticated}, NameIdentifier: {NameIdentifier})")]
    public static partial void ClaimIsNull(
        this ILogger logger,
        string claimType,
        bool authenticated,
        string? nameIdentifier);

    [LoggerMessage(
        EventId = 10026,
        Level = LogLevel.Debug,
        Message = "{SlackApiAction} in Background Slack Client (Count: {Count}, PlatformId: {PlatformId})")]
    public static partial void SlackApiAction(this ILogger logger, string slackApiAction, int count, string platformId);

    [LoggerMessage(
        EventId = 10030,
        Level = LogLevel.Debug,
        Message =
            "Creating trigger (Trigger Type: {TriggerType}, {PlatformRoomId}, Skill: (Id: {SkillId}, Name: {SkillName}))")]
    public static partial void CreatingTrigger(
        this ILogger logger,
        Type triggerType,
        string platformRoomId,
        int skillId,
        string skillName);

    [LoggerMessage(
        EventId = 10031,
        Level = LogLevel.Debug,
        Message =
            "Skill already has {TriggerType} trigger for room {PlatformRoomId} (Skill Id: {SkillId}, Name: {SkillName})")]
    public static partial void TriggerAlreadyExists(
        this ILogger logger,
        Type triggerType,
        string platformRoomId,
        int skillId,
        string skillName);

    [LoggerMessage(
        EventId = 10032,
        Level = LogLevel.Debug,
        Message =
            "Skill does not have {TriggerType} trigger for room {PlatformRoomId} (Skill Id: {SkillId}, Name: {SkillName})")]
    public static partial void TriggerDoesNotHave(
        this ILogger logger,
        Type triggerType,
        string platformRoomId,
        int skillId,
        string skillName);

    [LoggerMessage(
        EventId = 10033,
        Level = LogLevel.Debug,
        Message =
            "Running scheduled skill (Skill: (Id: {SkillId}, Name: {SkillName}), Scheduled Trigger Id: {ScheduledTriggerId}, Organization Id: {OrganizationId})")]
    public static partial void RunScheduledSkill(
        this ILogger logger,
        int skillId,
        string skillName,
        int scheduledTriggerId,
        int organizationId);

    [LoggerMessage(
        EventId = 10040,
        Level = LogLevel.Debug,
        Message =
            "Processing signal (Name: {SignalName}, Arguments: {SignalArguments}, Source Skill Id: {SourceSkillId})")]
    public static partial void ProcessingSignalStart(
        this ILogger logger,
        string signalName,
        string signalArguments,
        int sourceSkillId);

    [LoggerMessage(
        EventId = 10060,
        Level = LogLevel.Debug,
        Message = "User {BrainAction} data from brain (Key: {BrainKey}, Value: {BrainValue})")]
    public static partial void BrainAction(
        this ILogger logger,
        string brainAction,
        string brainKey,
        string? brainValue);

    [LoggerMessage(
        EventId = 10061,
        Level = LogLevel.Debug,
        Message = "Retrieve data from brain (Key: {BrainKey}, As Type: {DataType})")]
    public static partial void RetrieveBrainDataAsType(this ILogger logger, string brainKey, Type dataType);

    [LoggerMessage(
        EventId = 10070,
        Level = LogLevel.Debug,
        Message = "Create room (Name: {RoomName}, Private: {IsPrivateRoom}, ApiUrl: {ApiUrl}")]
    public static partial void CreateRoom(
        this ILogger logger,
        string roomName,
        bool isPrivateRoom,
        Uri apiUrl);

    [LoggerMessage(
        EventId = 10071,
        Level = LogLevel.Debug,
        Message = "Set {RoomProperty} to {RoomPropertyValue}, Room: {PlatformRoomId}, ApiUrl: {ApiUrl}")]
    public static partial void ChangeRoomProperty(
        this ILogger logger,
        string roomProperty,
        string roomPropertyValue,
        string platformRoomId,
        Uri apiUrl);

    [LoggerMessage(
        EventId = 10072,
        Level = LogLevel.Debug,
        Message = "Invite Users to Room (User Ids: {PlatformUserId}, Room: {PlatformRoomId}, ApiUrl: {ApiUrl}")]
    public static partial void InviteUsersToRoom(
        this ILogger logger,
        IEnumerable<string> platformUserId,
        string platformRoomId,
        Uri apiUrl);

    [LoggerMessage(
        EventId = 10080,
        Level = LogLevel.Debug,
        Message =
            "Member not found for name identifier (NameIdentifier: {NameIdentifier}, PlatformUserId: {PlatformUserId}) Creating member…")]
    public static partial void MemberNotFoundForNameIdentifier(
        this ILogger logger,
        string? nameIdentifier,
        string? platformUserId);

    [LoggerMessage(
        EventId = 10081,
        Level = LogLevel.Debug,
        Message =
            "Member not found for name identifier, but found for PlatformUserId (Id: {MemberId}, NameIdentifier: {NameIdentifier}, PlatformUserId: {PlatformUserId}) Updating member info…")]
    public static partial void MemberNotFoundForNameIdentifierButFoundForPlatformUserId(
        this ILogger logger,
        int memberId,
        string? nameIdentifier,
        string? platformUserId);

    [LoggerMessage(
        EventId = 10082,
        Level = LogLevel.Debug,
        Message =
            "Member created (Id: {MemberId}, NameIdentifier: {NameIdentifier}, PlatformUserId: {PlatformUserId})")]
    public static partial void MemberCreated(
        this ILogger logger,
        int memberId,
        string? nameIdentifier,
        string? platformUserId);

    [LoggerMessage(
        EventId = 10083,
        Level = LogLevel.Debug,
        Message =
            "Member updated (Id: {MemberId}, NameIdentifier: {NameIdentifier}, PlatformUserId: {PlatformUserId})")]
    public static partial void MemberUpdated(
        this ILogger logger,
        int memberId,
        string? nameIdentifier,
        string? platformUserId);

    [LoggerMessage(
        EventId = 10092,
        Level = LogLevel.Debug,
        Message =
            "User has no membership to organization (Id: {UserId}, PlatformUserId: {PlatformUserId}, PlatformId: {PlatformId}) Creating member…")]
    public static partial void UserHasNoMembershipToOrganization(
        this ILogger logger,
        int userId,
        string platformUserId,
        string platformId);

    [LoggerMessage(
        EventId = 10093,
        Level = LogLevel.Debug,
        Message =
            "Organization created (Id: {OrganizationId}, PlatformId: {PlatformId}, PlatformType: {PlatformType})")]
    public static partial void OrganizationCreated(
        this ILogger logger,
        int organizationId,
        string platformId,
        PlatformType platformType);
}
