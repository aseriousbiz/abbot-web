using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities.Filters;
using Serious.Abbot.Messages;
using Serious.Abbot.Serialization;
using Serious.EntityFrameworkCore;
using Serious.Filters;

namespace Serious.Abbot.Entities;

/// <summary>
/// A customer of the <see cref="Organization"/>.
/// </summary>
public class Customer : OrganizationEntityBase<Customer>, ITrackedEntity, IFilterableEntity<Customer>
{
    public Customer()
    {
        Metadata = new EntityList<CustomerMetadataField>();
    }

    // Special constructor called by EF Core.
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once UnusedMember.Local
    Customer(DbContext db)
    {
        Metadata = new EntityList<CustomerMetadataField>(db, this, nameof(Metadata));
    }

    /// <summary>
    /// The name of the customer.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Our property-bag for customer properties. These are set by us, not by customers.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public CustomerProperties Properties { get; set; } = new();

    /// <summary>
    /// The set of rooms that belong to this customer.
    /// </summary>
    public List<Room> Rooms { get; init; } = new();

    /// <summary>
    /// Custom metadata for the customer.
    /// </summary>
    public EntityList<CustomerMetadataField> Metadata { get; set; }

    /// <summary>
    /// The creator of the customer.
    /// </summary>
    public User Creator { get; set; } = null!;

    /// <summary>
    /// The date it was last modified.
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// The <see cref="User"/> that last modified this customer.
    /// </summary>
    public User ModifiedBy { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="User"/> that last modified this customer.
    /// </summary>
    public int ModifiedById { get; set; }

    /// <summary>
    /// Gets a list of all the <see cref="CustomerTagAssignment"/> entities that represent tags assigned to this customer.
    /// </summary>
    public List<CustomerTagAssignment> TagAssignments { get; set; } = new();

    /// <inheritdoc cref="IFilterableEntity{TEntity, TContext}.GetFilterItemQueries"/>
    public static IEnumerable<IFilterItemQuery<Customer>> GetFilterItemQueries() => CustomerFilters.CreateFilters();

    /// <summary>
    /// Looks at every <see cref="Room"/> for this customer and returns the latest date that a message was received or
    /// a message interaction occurred.
    /// </summary>
    public DateTime? LastMessageActivityUtc => Rooms
        .Select(r => r.LastMessageActivityUtc)
        .Where(lad => lad != null)
        .OrderDescending()
        .FirstOrDefault();

    public Room? GetPrimaryRoom() => Rooms
        .OrderByDescending(r => r.Shared)
        .ThenBy(r => r.Created).FirstOrDefault();
}

/// <summary>
/// Properties of a customer.
/// </summary>
public record CustomerProperties : JsonSettings
{
    /// <summary>
    /// Gets or sets the email address for the primary contact for this customer.
    /// </summary>
    public string? PrimaryContactEmail { get; set; }
}

public static class CustomerExtensions
{
    public static CustomerInfo ToCustomerInfo(this Customer entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Rooms = entity.Rooms.Select(r => new PlatformRoom(r.PlatformRoomId, r.Name)).ToList(),
        Tags = entity.TagAssignments.Select(t => t.Tag.Name).ToList(),
        Metadata = entity.Metadata.ToDictionary(m => m.MetadataField.Name, m => m.Value ?? m.MetadataField.DefaultValue)
    };
}
