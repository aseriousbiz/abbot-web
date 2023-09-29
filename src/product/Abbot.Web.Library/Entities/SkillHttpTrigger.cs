using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// A trigger that can be used to call the skill by an external system
/// via an HTTP request.
/// </summary>
public class SkillHttpTrigger : SkillTrigger<SkillHttpTrigger>
{
    /// <summary>
    /// The Api Token required in order to send HTTP requests to the <see cref="Skill"/>.
    /// </summary>
    public string ApiToken { get; set; } = null!;

    public override AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var auditEvent = CreateAuditEventInstance<HttpTriggerChangeEvent>();

        auditEvent.Description = $"{auditOperation} HTTP trigger `{Name}` for skill `{Skill.Name}`.";
        auditEvent.ApiToken = ApiToken;

        return auditEvent;
    }
}
