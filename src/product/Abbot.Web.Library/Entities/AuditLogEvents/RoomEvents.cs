using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Event raised when someone links a room to an external resource.
/// </summary>
public abstract class RoomLinkEvent : LegacyAuditEvent
{
    /// <summary>
    /// The type of the link
    /// </summary>
    [Column(TypeName = "text")]
    public RoomLinkType LinkType { get; set; }

    /// <summary>
    /// The external ID that the room is linked to.
    /// </summary>
    public string ExternalId { get; set; } = null!;
}

public class RoomLinkedEvent : RoomLinkEvent
{
}

public class RoomUnlinkedEvent : RoomLinkEvent
{
}
