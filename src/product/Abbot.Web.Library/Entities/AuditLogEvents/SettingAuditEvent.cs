using System;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event raised when an auditable setting is changed.
/// </summary>
public class SettingAuditEvent : LegacyAuditEvent
{
    public override bool HasDetails => false; // TODO: SettingAuditInfo is not null;
}

/// <summary>
/// Information about the <see cref="SettingAuditEvent"/>.
/// </summary>
/// <param name="AuditEventType">The <see cref="AuditOperation"/>.</param>
/// <param name="Scope">The setting scope.</param>
/// <param name="Name">The setting name.</param>
/// <param name="Value">The setting value.</param>
/// <param name="Expiry">The optional setting expiration time (UTC).</param>
public record SettingAuditInfo(
    AuditOperation AuditEventType,
    string Scope,
    string Name,
    string Value,
    DateTime? Expiry)
{
    public static SettingAuditInfo Create(AuditOperation auditOperation, Setting setting) =>
        new(auditOperation,
            setting.Scope,
            setting.Name,
            setting.Value,
            setting.Expiry);
}
