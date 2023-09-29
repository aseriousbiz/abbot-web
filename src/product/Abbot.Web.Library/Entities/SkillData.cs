using System.ComponentModel.DataAnnotations.Schema;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// A piece of data stored by the skill.
/// </summary>
public class SkillData : TrackedEntityBase<SkillData>, ISkillChildEntity, IAuditableEntity
{
    /// <summary>
    /// The lookup key for the data item.
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// The value of the data item.
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// The <see cref="Skill"/> this data item belongs to.
    /// </summary>
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Skill"/> this data item belongs to.
    /// </summary>
    [Column("SkillId")]
    public int SkillId { get; set; }

    /// <summary>
    /// The scope of the skill data item - per skill, per user, per conversation...
    /// </summary>
    [Column(TypeName = "text")]
    public SkillDataScope Scope { get; set; } = SkillDataScope.Organization;

    /// <summary>
    /// Id that tracks the state and context of chat messages.
    /// If the skill scope is set to user, this is the UserId
    /// If the skill scope is set to Conversation, this is the ConversationId
    /// If the skill scope is set to Room, this is the RoomId
    /// </summary>
    public string? ContextId { get; set; }

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        return new SkillAuditEvent
        {
            Description = $"{auditOperation} data item for skill `{Skill.Name}`."
        };
    }
}
