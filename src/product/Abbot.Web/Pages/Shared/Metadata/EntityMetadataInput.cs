using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

/// <summary>
/// Input for Room Metadata Input.
/// </summary>
public class EntityMetadataInput
{
    public static EntityMetadataInput FromMetadataField(MetadataField metadataField, Room room)
    {
        var existing = metadataField.RoomMetadataFields.SingleOrDefault(m => m.RoomId == room.Id);

        return new EntityMetadataInput
        {
            Name = metadataField.Name,
            DefaultValue = metadataField.DefaultValue,
            Value = existing?.Value,
        };
    }

    public static EntityMetadataInput FromMetadataField(MetadataField metadataField, IEntity entity)
    {
        var existingValue = metadataField.Type switch
        {
            MetadataFieldType.Room => metadataField.RoomMetadataFields.SingleOrDefault(m => m.RoomId == entity.Id)?.Value,
            MetadataFieldType.Customer => metadataField.CustomerMetadataFields.SingleOrDefault(m => m.CustomerId == entity.Id)?.Value,
            _ => throw new InvalidOperationException($"No metadata field found for {entity}.")
        };

        return new EntityMetadataInput
        {
            Name = metadataField.Name,
            DefaultValue = metadataField.DefaultValue,
            Value = existingValue,
        };
    }

    public string? Value { get; init; }

    public required string Name { get; init; }

    public string? DefaultValue { get; init; }

    public void Deconstruct(out string name, out string? value)
    {
        name = Name;
        value = Value;
    }
}
