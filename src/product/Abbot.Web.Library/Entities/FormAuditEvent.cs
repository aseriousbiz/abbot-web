using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

public class FormAuditEvent : LegacyAuditEvent
{
    [NotMapped]
    public override bool HasDetails => true;
}

public record FormAuditEventProperties(string FormKey);
