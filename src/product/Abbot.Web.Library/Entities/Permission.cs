using System;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a member's permission to execute a skill.
/// </summary>
#pragma warning disable CA1711
public class Permission
#pragma warning restore
{
    /// <summary>
    /// The permission level for this permission.
    /// </summary>
    public Capability Capability { get; set; }

    /// <summary>
    /// Id for a <see cref="Member"/> that is allowed to execute the skill.
    /// </summary>
    public int MemberId { get; set; }

    /// <summary>
    /// The <see cref="Member"/> that is allowed to execute the skill.
    /// </summary>
    public Member Member { get; set; } = null!;

    /// <summary>
    /// Id for a <see cref="Skill"/> that the member is allowed to execute.
    /// </summary>
    public int SkillId { get; set; }

    /// <summary>
    /// The <see cref="Skill"/> that the member is allowed to execute.
    /// </summary>
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// The date (UTC) when this entity was created.
    /// </summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// The <see cref="User"/> that set this permission.
    /// </summary>
    public User Creator { get; set; } = null!;
}
