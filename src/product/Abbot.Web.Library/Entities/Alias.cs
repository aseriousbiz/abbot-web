using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// Provides a shortcut for calling another skill with a preconfigured
/// set of arguments.
/// </summary>
public class Alias : SkillEntityBase<Alias>, IAuditableEntity
{
    /// <summary>
    /// The skill to call.
    /// </summary>
    public string TargetSkill { get; set; } = null!;

    /// <summary>
    /// The arguments to pass to the called skill.
    /// </summary>
    public string TargetArguments { get; set; } = string.Empty;

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var arguments = TargetArguments is { Length: 0 }
            ? "_no arguments_"
            : $"arguments `{TargetArguments}`";
        var description = $"{auditOperation} alias `{Name}` that targets skill `{TargetSkill}` with {arguments}.";

        return new AuditEvent
        {
            Type = new("Alias", auditOperation),
            Description = description
        };
    }
}
