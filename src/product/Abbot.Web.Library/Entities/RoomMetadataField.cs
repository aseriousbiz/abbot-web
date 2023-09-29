namespace Serious.Abbot.Entities;

/// <summary>
/// Custom Metadata about a <see cref="Room"/>
/// </summary>
public class RoomMetadataField : TrackedEntityBase<RoomMetadataField>
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
    /// The <see cref="Room"/>.
    /// </summary>
    public Room Room { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Room"/> the <see cref="MetadataField" /> is attached to.
    /// </summary>
    public int RoomId { get; set; }
}
