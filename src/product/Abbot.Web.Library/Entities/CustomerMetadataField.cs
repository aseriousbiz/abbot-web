namespace Serious.Abbot.Entities;

/// <summary>
/// Custom Metadata about a <see cref="Customer"/>
/// </summary>
public class CustomerMetadataField : TrackedEntityBase<CustomerMetadataField>
{
    /// <summary>
    /// The <see cref="MetadataField"/> this is a value for.
    /// </summary>
    public MetadataField MetadataField { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="MetadataField"/> this is a value for.
    /// </summary>
    public int MetadataFieldId { get; set; }

    /// <summary>
    /// The value of this room metadata. If <c>null</c>, then the value is <see cref="MetadataField.DefaultValue" />
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// The <see cref="Customer"/>.
    /// </summary>
    public Customer Customer { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Customer"/> the <see cref="MetadataField" /> is attached to.
    /// </summary>
    public int CustomerId { get; set; }
}
