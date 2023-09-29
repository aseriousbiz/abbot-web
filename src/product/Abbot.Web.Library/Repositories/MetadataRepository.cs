using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

/// <summary>
/// A repository for managing Metadata fields.
/// </summary>
public interface IMetadataRepository : IRepository<MetadataField>
{
    /// <summary>
    /// Retrieve all <see cref="MetadataField"/> entities of the given type.
    /// </summary>
    /// <param name="fieldType">The type of metadata fields to retrieve.</param>
    /// <param name="organization">The organization the fields belongs to.</param>
    Task<IReadOnlyList<MetadataField>> GetAllAsync(MetadataFieldType fieldType, Organization organization);

    /// <summary>
    /// Retrieves a metadata field by name.
    /// </summary>
    /// <param name="fieldType">The type of metadata fields to retrieve.</param>
    /// <param name="name">The name of the field.</param>
    /// <param name="organization">The organization the field belongs to.</param>
    /// <returns></returns>
    Task<MetadataField?> GetByNameAsync(MetadataFieldType fieldType, string name, Organization organization);

    /// <summary>
    /// Creates a metadata field.
    /// </summary>
    /// <param name="fieldType">The type of metadata fields to retrieve.</param>
    /// <param name="name">The name of the field.</param>
    /// <param name="defaultValue">The default value for the field.</param>
    /// <param name="actor">The <see cref="Member"/> making the change.</param>
    /// <param name="organization">The organization the field belongs to.</param>
    /// <returns>The created <see cref="MetadataField"/>.</returns>
    Task<MetadataField> CreateMetadataFieldAsync(
        MetadataFieldType fieldType,
        string name,
        string? defaultValue,
        Member actor,
        Organization organization);

    /// <summary>
    /// Updates a metadata field.
    /// </summary>
    /// <param name="fieldType">The type of metadata fields to retrieve.</param>
    /// <param name="name">The name of the field.</param>
    /// <param name="newName">The new name for the field.</param>
    /// <param name="newDefaultValue">The new default value for the field.</param>
    /// <param name="actor">The <see cref="Member"/> making the change.</param>
    /// <param name="organization">The organization the field belongs to.</param>
    /// <returns>The updated <see cref="MetadataField"/> or null if it was not found.</returns>
    Task<MetadataField?> UpdateMetadataFieldAsync(
        MetadataFieldType fieldType,
        string name,
        string newName,
        string? newDefaultValue,
        Member actor,
        Organization organization);

    /// <summary>
    /// Resolves the values for all metadata fields for the room. In the cases where the field is not set for the
    /// room, falls back to the default value.
    /// </summary>
    /// <param name="room">The room to retrieve metadata for.</param>
    Task<IReadOnlyDictionary<string, string?>> ResolveValuesForRoomAsync(Room room);

    /// <summary>
    /// Resolves the values for all metadata fields for the customer. In the cases where the field is not set for the
    /// room, falls back to the default value.
    /// </summary>
    /// <param name="customer">The room to retrieve metadata for.</param>
    Task<IReadOnlyDictionary<string, string?>> ResolveValuesForCustomerAsync(Customer customer);

    /// <summary>
    /// Updates the room metadata to match the provided dictionary.
    /// </summary>
    /// <param name="room">The room to update.</param>
    /// <param name="metadata">The final state of the metadata.</param>
    /// <param name="actor">The <see cref="Member"/> making the change.</param>
    Task UpdateRoomMetadataAsync(Room room, IDictionary<string, string?> metadata, Member actor);

    /// <summary>
    /// Updates the customer metadata to match the provided dictionary.
    /// </summary>
    /// <param name="customer">The customer to update.</param>
    /// <param name="metadata">The final state of the metadata.</param>
    /// <param name="actor">The <see cref="Member"/> making the change.</param>
    Task UpdateCustomerMetadataAsync(Customer customer, IDictionary<string, string?> metadata, Member actor);
}

public class MetadataRepository : OrganizationScopedRepository<MetadataField>, IMetadataRepository
{
    readonly IClock _clock;

    public MetadataRepository(AbbotContext db, IAuditLog auditLog, IClock clock) : base(db, auditLog)
    {
        _clock = clock;
    }

    protected override DbSet<MetadataField> Entities => Db.MetadataFields;

    public async Task<IReadOnlyList<MetadataField>> GetAllAsync(MetadataFieldType fieldType, Organization organization)
    {
        return await GetQueryable(fieldType, organization).ToListAsync();
    }

    public async Task<MetadataField?> GetByNameAsync(MetadataFieldType fieldType, string name, Organization organization)
    {
        return await GetQueryable(fieldType, organization).FirstOrDefaultAsync(e => e.Name == name);
    }

    public async Task<MetadataField> CreateMetadataFieldAsync(
        MetadataFieldType fieldType,
        string name,
        string? defaultValue,
        Member actor,
        Organization organization)
    {
        var field = new MetadataField
        {
            Name = name,
            DefaultValue = defaultValue,
            Organization = organization,
            OrganizationId = organization.Id,
            Modified = _clock.UtcNow,
            Created = _clock.UtcNow,
            Type = fieldType,
        };

        return await CreateAsync(field, actor.User);
    }

    public async Task<MetadataField?> UpdateMetadataFieldAsync(
        MetadataFieldType fieldType,
        string name,
        string newName,
        string? newDefaultValue,
        Member actor,
        Organization organization)
    {
        var field = await GetByNameAsync(fieldType, name, organization);
        if (field is null)
        {
            return null;
        }

        field.Name = newName;
        field.DefaultValue = newDefaultValue;
        field.Modified = _clock.UtcNow;
        await UpdateAsync(field, actor.User);
        return field;
    }

    public async Task<IReadOnlyDictionary<string, string?>> ResolveValuesForRoomAsync(Room room)
    {
        var fields = await GetQueryable(MetadataFieldType.Room, room.Organization).ToListAsync();
        if (!room.Metadata.IsLoaded)
        {
            await Db.Entry(room).Collection(r => r.Metadata).LoadAsync();
        }
        return fields.ToDictionary(f => f.Name, f => f.ResolveValue(room));
    }

    public async Task<IReadOnlyDictionary<string, string?>> ResolveValuesForCustomerAsync(Customer customer)
    {
        var fields = await GetQueryable(MetadataFieldType.Customer, customer.Organization).ToListAsync();
        if (!customer.Metadata.IsLoaded)
        {
            await Db.Entry(customer).Collection(c => c.Metadata).LoadAsync();
        }
        return fields.ToDictionary(f => f.Name, f => f.ResolveValue(customer));
    }

    public async Task UpdateRoomMetadataAsync(Room room, IDictionary<string, string?> metadata, Member actor)
    {
        var metadataFields = await GetQueryable(MetadataFieldType.Room, room.Organization).ToListAsync();

        foreach (var (name, value) in metadata)
        {
            var metadataField = metadataFields.Single(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var roomMetadata = metadataField.RoomMetadataFields.SingleOrDefault(f => f.RoomId == room.Id);

            if (value is null)
            {
                if (roomMetadata is not null)
                {
                    room.Metadata.Remove(roomMetadata);
                    room.Modified = _clock.UtcNow;
                }
            }
            else
            {
                if (roomMetadata is null)
                {
                    roomMetadata = new RoomMetadataField
                    {
                        MetadataFieldId = metadataField.Id,
                        Room = room,
                        RoomId = room.Id,
                        MetadataField = metadataField,
                        Creator = actor.User,
                        Created = _clock.UtcNow,
                    };

                    room.Metadata.Add(roomMetadata);
                }
                roomMetadata.Value = value;
                roomMetadata.ModifiedBy = actor.User;
                roomMetadata.Modified = _clock.UtcNow;
                room.Modified = _clock.UtcNow;
            }
        }

        await Db.SaveChangesAsync();
    }

    public async Task UpdateCustomerMetadataAsync(Customer customer, IDictionary<string, string?> metadata, Member actor)
    {
        var metadataFields = await GetQueryable(MetadataFieldType.Customer, customer.Organization).ToListAsync();

        foreach (var (name, value) in metadata)
        {
            var metadataField = metadataFields.Single(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var customerMetadata = metadataField.CustomerMetadataFields.SingleOrDefault(f => f.CustomerId == customer.Id);

            if (value is null)
            {
                if (customerMetadata is not null)
                {
                    customer.Metadata.Remove(customerMetadata);
                    customer.Modified = _clock.UtcNow;
                }
            }
            else
            {
                if (customerMetadata is null)
                {
                    customerMetadata = new CustomerMetadataField
                    {
                        MetadataFieldId = metadataField.Id,
                        Customer = customer,
                        CustomerId = customer.Id,
                        MetadataField = metadataField,
                        Creator = actor.User,
                        Created = _clock.UtcNow,
                    };

                    customer.Metadata.Add(customerMetadata);
                }
                customerMetadata.Value = value;
                customerMetadata.ModifiedBy = actor.User;
                customerMetadata.Modified = _clock.UtcNow;
                customer.Modified = _clock.UtcNow;
            }
        }

        await Db.SaveChangesAsync();
    }

    IQueryable<MetadataField> GetQueryable(MetadataFieldType fieldType, Organization organization)
    {
        var queryable = base.GetQueryable(organization)
            .Where(m => m.Type == fieldType);

        queryable = fieldType switch
        {
            MetadataFieldType.Room => queryable.Include(m => m.RoomMetadataFields),
            MetadataFieldType.Customer => queryable.Include(m => m.CustomerMetadataFields),
            _ => queryable
        };

        return queryable;
    }
}
