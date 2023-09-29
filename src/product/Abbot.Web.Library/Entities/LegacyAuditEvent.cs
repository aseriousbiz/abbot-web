using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

public abstract class LegacyAuditEvent : AuditEventBase
{
    protected LegacyAuditEvent()
    {
        Discriminator = GetType().Name;
    }

    /// <summary>
    /// The name of the room where the event occurred.
    /// </summary>
    [Column(nameof(Room))]
    public string? Room { get; set; }

    /// <summary>
    /// The platform specific Id of the room where the event occurred.
    /// </summary>
    [Column(nameof(RoomId))]
    public string? RoomId { get; set; }
}
