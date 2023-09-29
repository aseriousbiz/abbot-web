using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Serious.EntityFrameworkCore;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a Hub, which is a group of <see cref="Conversation"/>s
/// across multiple <see cref="Room"/>s
/// that are managed from within a single <see cref="Room"/>.
/// </summary>
public class Hub : OrganizationEntityBase<Hub>
{
    public Hub()
    {
        AttachedRooms = new EntityList<Room>();
    }

    Hub(DbContext db)
    {
        AttachedRooms = new EntityList<Room>(db, this, nameof(AttachedRooms));
    }

    /// <summary>
    /// Gets or sets the name of the hub.
    /// This defaults to the name of the <see cref="Room"/> that the hub was created in.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or inits the ID of the management <see cref="Room"/> for this <see cref="Hub"/>.
    /// </summary>
    public required int RoomId { get; init; }

    /// <summary>
    /// Gets or inits the management <see cref="Room"/> for this <see cref="Hub"/>.
    /// </summary>
    public required Room Room { get; init; }

    public EntityList<Room> AttachedRooms { get; set; }
}
