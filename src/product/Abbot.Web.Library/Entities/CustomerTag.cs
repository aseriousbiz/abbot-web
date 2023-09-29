using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a tag that can be attached to a customer.
/// </summary>
public class CustomerTag : TrackedOrganizationEntityBase<CustomerTag>
{
    /// <summary>
    /// The name of the tag.
    /// </summary>
    [Column(TypeName = "citext")]
    public required string Name { get; set; }

    /// <summary>
    /// Arbitrary properties associated with the tag.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public CustomerTagProperties Properties { get; set; } = new();

    /// <summary>
    /// The list of <see cref="CustomerTagAssignment"/>s representing the customers that have this tag assigned.
    /// </summary>
    public List<CustomerTagAssignment> Assignments { get; set; } = new();
}

/// <summary>
/// Properties associated with a <see cref="CustomerTag"/>.
/// </summary>
public record CustomerTagProperties : JsonSettings;

/// <summary>
/// Represents the assignment of a <see cref="CustomerTag"/> to a <see cref="Customer"/>.
/// </summary>
public class CustomerTagAssignment : TrackedEntityBase<CustomerTagAssignment>
{
    /// <summary>
    /// The ID of the <see cref="Customer"/> that has this tag assigned.
    /// </summary>
    public required int CustomerId { get; set; }

    /// <summary>
    /// The <see cref="Customer"/> that has this tag assigned.
    /// </summary>
    public required Customer Customer { get; set; }

    /// <summary>
    /// The ID of the <see cref="Tag"/> that is assigned to the customer.
    /// </summary>
    public required int TagId { get; set; }

    /// <summary>
    /// The <see cref="Tag"/> that is assigned to the customer.
    /// </summary>
    public required CustomerTag Tag { get; set; }
}
