using System;
using System.Globalization;
using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Messages;

/// <summary>
/// Context for the skill runner.
/// </summary>
public class SkillRunnerInfo
{
    /// <summary>
    /// The Id of the skill in the database.
    /// </summary>
    public int SkillId { get; init; }

    /// <summary>
    /// The Id of the user calling the skill. This is the Id in the Abbot database, not the Platform User Id.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// The Id of the member calling the skill. This is the Id in the Abbot database, not the Platform User Id.
    /// </summary>
    public int MemberId { get; init; }

    /// <summary>
    /// The Id of the room where this skill was called. This is the Id in the Abbot database.
    /// </summary>
    public int? RoomId { get; set; }

    /// <summary>
    /// The Id of the conversation this skill is a part of, if any. This is a convenience field, coming
    /// from the Conversation object.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Skill data scope information, so we can verify if a user is allowed to interact with a skill
    /// </summary>
    public SkillDataScope Scope { get; init; }

    /// <summary>
    /// Contains the source code for the skill to run. Only applies to non-compiled languages such as
    /// our Python and JavaScript runners.
    /// </summary>
    /// <remarks>
    /// For C# skills, this is a hash of the skill code used as a cache key.
    /// </remarks>
    public string Code { get; init; } = null!;

    /// <summary>
    /// For C# skills only, the cache key used to retrieve the compiled skill assembly.
    /// </summary>
    public string CacheKey => Code;

    /// <summary>
    /// The language of this skill
    /// </summary>
    public CodeLanguage Language { get; init; }

    /// <summary>
    /// The timestamp of the source message that triggered this skill.
    /// </summary>
    public long Timestamp { get; init; }

    /// <summary>
    /// A reference that allows the skill runner to reply to an attached chat room.
    /// </summary>
    /// <remarks>
    /// We need to keep passing this until we can remove the need for it from the JS and Python runners.
    /// </remarks>
    [Obsolete("SkillInfo.Room and SkillInfo.Thread has the info we need")]
    public ConversationReference? ConversationReference { get; init; }

    /// <summary>
    /// Used to identify a skill run with the audit log entry.
    /// </summary>
    public Guid AuditIdentifier { get; init; }

    /// <summary>
    /// Id that tracks the state and context of chat messages.
    /// If the skill scope is set to user, this is the UserId
    /// If the skill scope is set to Conversation, this is the ConversationId
    /// If the skill scope is set to Room, this is the RoomId
    /// </summary>
    public string? ContextId => this.ToContextId();
}

public static class ScopeExtensions
{
    public static string? ToContextId(this SkillRunnerInfo info) => info.Scope switch
    {
        SkillDataScope.Organization => null,
        SkillDataScope.Room => info.RoomId?.ToString(CultureInfo.InvariantCulture),
        SkillDataScope.Conversation => info.ConversationId,
        SkillDataScope.User => info.UserId.ToString(CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(info), $"Invalid scope {info.Scope}"),
    };
}
