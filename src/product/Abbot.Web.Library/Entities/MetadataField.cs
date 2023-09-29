using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.EntityFrameworkCore;

namespace Serious.Abbot.Entities;

/// <summary>
/// A Name Value Pair that can be attached as Metadata to another entity such as a <see cref="Room"/>.
/// </summary>
public class MetadataField : OrganizationEntityBase<MetadataField>, ITrackedEntity
{
    public MetadataField()
    {
        RoomMetadataFields = new EntityList<RoomMetadataField>();
        CustomerMetadataFields = new EntityList<CustomerMetadataField>();
    }

    // Special constructor called by EF Core.
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once UnusedMember.Local
    MetadataField(DbContext db)
    {
        RoomMetadataFields = new EntityList<RoomMetadataField>(db, this, nameof(RoomMetadataFields));
        CustomerMetadataFields = new EntityList<CustomerMetadataField>(db, this, nameof(RoomMetadataFields));
    }

    /// <summary>
    /// Identifies the metadata field. Unique per organization for a given scope (at this moment, the only scope is
    /// Room metadata).
    /// </summary>
    [Column(TypeName = "citext")]
    public required string Name { get; set; } // Unique per organization+type.

    /// <summary>
    /// Determines whether this metadata can be used with rooms or customers.
    /// </summary>
    public required MetadataFieldType Type { get; set; }

    /// <summary>
    /// The default value for the metadata field. If <c>nulL</c>, then a value is required for the field.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// The user that created this entity.
    /// </summary>
    public User Creator { get; set; } = null!;

    /// <summary>
    /// The date this entity was last modified.
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// The user that last modified this entity.
    /// </summary>
    public User ModifiedBy { get; set; } = null!;

    /// <summary>
    /// The Id of the user that last modified this entity.
    /// </summary>
    public int ModifiedById { get; set; }

    /// <summary>
    /// The rooms where this metadata field is used.
    /// </summary>
    public EntityList<RoomMetadataField> RoomMetadataFields { get; set; }

    /// <summary>
    /// The rooms where this metadata field is used.
    /// </summary>
    public EntityList<CustomerMetadataField> CustomerMetadataFields { get; set; }

    /// <summary>
    /// Resolves the value of this metadata field for the specified room. If the room does not have a value, then
    /// the default value is used.
    /// </summary>
    /// <param name="room">The room.</param>
    public string? ResolveValue(Room room)
    {
        return room.Metadata.SingleOrDefault(r => r.MetadataFieldId == Id)?.Value ?? DefaultValue;
    }

    /// <summary>
    /// Resolves the value of this metadata field for the specified customer. If the customer does not have a value, then
    /// the default value is used.
    /// </summary>
    /// <param name="customer">The room.</param>
    public string? ResolveValue(Customer customer)
    {
        return customer.Metadata.SingleOrDefault(r => r.MetadataFieldId == Id)?.Value ?? DefaultValue;
    }
}

/// <summary>
/// The type of entity this metadata type is for.
/// </summary>
public enum MetadataFieldType
{
    /// <summary>
    /// This field can be used with rooms.
    /// </summary>
    Room,

    /// <summary>
    /// This field can be used with customers.
    /// </summary>
    Customer,
}
