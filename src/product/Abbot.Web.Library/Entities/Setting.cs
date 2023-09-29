using System.ComponentModel.DataAnnotations.Schema;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// Simple name value setting.
/// </summary>
public class Setting : TrackedEntityBase<Setting>, IAuditableEntity
{
    /// <summary>
    /// Name of the setting.
    /// </summary>
    [Column(TypeName = "citext")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Value of the setting.
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// The scope of the setting.
    /// </summary>
    public string Scope { get; set; } = null!;

    /// <summary>
    /// The UTC time at which the setting expires.
    /// After this time, it will not be returned by <see cref="ISettingsManager.GetAsync"/>
    /// and may be eligible for deletion by a background job.
    /// </summary>
    public DateTime? Expiry { get; set; }

    /// <summary>
    /// Creates an audit event of a type specific to the entity.
    /// </summary>
    /// <param name="auditOperation">The type of audit event.</param>
    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var description = $"{auditOperation} setting `{Name}` with value `{Value.TruncateToLength(60, appendEllipses: true)}`.";
        var details = $"{auditOperation} setting `{Name}` with value `{Value}`.";
        return new SettingAuditEvent
        {
            Properties = SettingAuditInfo.Create(auditOperation, this),
            Description = description,
            Details = details,
        };
    }
}
