using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents an assignment of a user in a room to a role within that room.
/// </summary>
public class RoomAssignment : TrackedEntityBase<RoomAssignment>
{
    /// <summary>
    /// The ID of the <see cref="Entities.Room"/> in which the <see cref="Member"/> has been assigned.
    /// </summary>
    public int RoomId { get; set; }

    /// <summary>
    /// The <see cref="Entities.Room"/> in which the <see cref="Member"/> has been assigned.
    /// </summary>
    public Room Room { get; set; } = null!;

    /// <summary>
    /// The ID of the <see cref="Member"/> that has been assigned.
    /// </summary>
    public int MemberId { get; set; }

    /// <summary>
    /// The <see cref="Member"/> that has been assigned.
    /// </summary>
    public Member Member { get; set; } = null!;

    /// <summary>
    /// The role that the user has been assigned to in the room.
    /// </summary>
    [Column(TypeName = "text")]
    public RoomRole Role { get; set; }
}
