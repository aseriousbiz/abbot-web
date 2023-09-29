using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using Serious.Filters;

namespace Serious.Abbot.Repositories;

/// <summary>
/// A repository for managing <see cref="Customer"/> records.
/// </summary>
public class CustomerRepository : OrganizationScopedRepository<Customer>
{
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;

    public CustomerRepository(
        AbbotContext db,
        IAuditLog auditLog,
        IAnalyticsClient analyticsClient,
        IClock clock)
        : base(db, auditLog)
    {
        _analyticsClient = analyticsClient;
        _clock = clock;
    }

    protected override DbSet<Customer> Entities => Db.Customers;

    public async Task<IReadOnlyList<Customer>> GetAllWithRoomsAsync(
        Organization organization,
        IReadOnlyList<string> segments)
    {
        var normalizedSegments = segments.Select(s => s.ToLowerInvariant()).ToList();
        var query = GetQueryable(organization)
            .Where(c => normalizedSegments.Count == 0 || c.TagAssignments.Any(a => normalizedSegments.Contains(a.Tag.Name.ToLower())))
            .Include(c => c.Rooms)
            .OrderBy(c => c.Name.ToLower());
        return await query.ToListAsync();
    }

    public async Task<IPaginatedList<Customer>> GetAllAsync(
        Organization organization,
        FilterList filter,
        int pageNumber,
        int pageSize)
    {
        var query = GetQueryable(organization)
            .OrderBy(c => c.Name.ToLower())
            .ApplyFilter(filter, defaultField: "search");
        return await PaginatedList.CreateAsync(query, pageNumber, pageSize);
    }

    public async Task<Customer?> GetCustomerByNameAsync(string name, Organization organization)
    {
        return await GetQueryable(organization)
            .Where(t => EF.Functions.ILike(t.Name, name))
            .SingleOrDefaultAsync();
    }

    public async Task<CustomerTag?> GetCustomerSegmentByNameAsync(string name, Organization organization)
    {
        return await GetCustomerSegmentQueryable(organization)
            .Where(t => EF.Functions.ILike(t.Name, name))
            .SingleOrDefaultAsync();
    }

    public async Task<IReadOnlyList<CustomerTag>> GetCustomerSegmentsForTypeAheadQueryAsync(
        Organization organization,
        string? segmentNameFilter,
        int limit)
    {
        var query = GetCustomerSegmentQueryable(organization)
            .Where(s => segmentNameFilter == null || EF.Functions.ILike(s.Name!, $"%{segmentNameFilter}%"))
            .OrderBy(s => s.Name);

        var limitedQuery = limit > 0
            ? query.Take(limit)
            : query;

        return await limitedQuery.ToListAsync();
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersForTypeAheadQueryAsync(
        Organization organization,
        string? nameFilter,
        int limit)
    {
        var query = GetQueryable(organization)
            .Where(s => nameFilter == null || EF.Functions.ILike(s.Name!, $"%{nameFilter}%"))
            .OrderBy(s => s.Name);

        var limitedQuery = limit > 0
            ? query.Take(limit)
            : query;

        return await limitedQuery.ToListAsync();
    }

    public async Task<IReadOnlyList<EntityLookupResult<CustomerTag, string>>> GetCustomerSegmentsByNamesAsync(
        IReadOnlyList<string> segmentNames,
        Id<Organization> organizationId)
    {
        var normalizedSegmentNames = segmentNames.Select(t => t.ToUpperInvariant()).ToList();
        var existingSegments = await Db.CustomerTags
            .Where(t => normalizedSegmentNames.Contains(t.Name.ToUpper()))
            .Where(t => t.OrganizationId == organizationId)
            .ToDictionaryAsync(t => t.Name, StringComparer.InvariantCultureIgnoreCase);

        EntityLookupResult<CustomerTag, string> GatherResult(string segmentName)
        {
            var exists = existingSegments.TryGetValue(segmentName, out var entity);
            var resultType = exists
                ? EntityResultType.Success
                : EntityResultType.NotFound;
            return new EntityLookupResult<CustomerTag, string>(resultType, segmentName, entity, null);
        }

        return segmentNames.Select(GatherResult).ToList();
    }

    /// <summary>
    /// Retrieves all the <see cref="CustomerTag"/> entities that match the passed in names, creating
    /// new ones if they don't exist.
    /// </summary>
    /// <param name="segmentNames">The set of segment names to retrieve or create.</param>
    /// <param name="actor">The <see cref="Member"/> retrieving or creating the tags.</param>
    /// <param name="organization">The organization the tags belong to.</param>
    /// <returns>The set of <see cref="CustomerTag"/> entities associated with the tag names.</returns>
    public async Task<IReadOnlyList<CustomerTag>> GetOrCreateSegmentsByNamesAsync(
        IReadOnlyList<string> segmentNames,
        Member actor,
        Organization organization)
    {
        var results = await GetCustomerSegmentsByNamesAsync(segmentNames, organization);
        var segmentsToCreate = results
            .Where(r => !r.IsSuccess)
            .Where(r => Tag.IsValidTagName(r.Key, allowGenerated: false))
            .Select(result => new CustomerTag
            {
                Name = result.Key,
                Created = _clock.UtcNow,
                Creator = actor.User,
                ModifiedBy = actor.User,
                Modified = _clock.UtcNow,
                Organization = organization,
                OrganizationId = organization.Id,
            })
            .ToList();

        await Db.CustomerTags.AddRangeAsync(segmentsToCreate);
        await Db.SaveChangesAsync();

        var createdSegmentNames = segmentsToCreate.Select(t => t.Name).Humanize();

        var noun = segmentsToCreate.Count == 1 ? "segment" : "segments";

        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("CustomerSegment", "Created"),
                Actor = actor,
                Organization = organization,
                Description = $"Created customer {noun} {createdSegmentNames}.",
            });

        return results.Select(r => r.Entity).WhereNotNull().Union(segmentsToCreate).ToList();
    }

    public async Task<Customer> CreateCustomerAsync(
        string name,
        IEnumerable<Room> rooms,
        Member actor,
        Organization organization,
        string? contactEmail)
    {
        var customer = new Customer
        {
            Name = name,
            Properties = new CustomerProperties()
            {
                PrimaryContactEmail = contactEmail,
            },
            Organization = organization,
            OrganizationId = organization.Id,
        };

        customer.Rooms.AddRange(rooms);

        var created = await CreateAsync(customer, actor.User);

        await LogActivityAsync(new[] { created }, "create customer page", "Created", actor, organization);
        return created;
    }

    public async Task UpdateCustomerAsync(
        Customer customer,
        string name,
        IEnumerable<Room> rooms,
        Member actor,
        Organization organization)
    {
        customer.Name = name;
        customer.Rooms.Clear();
        customer.Rooms.AddRange(rooms);

        await UpdateAsync(customer, actor.User);

        await LogActivityAsync(new[] { customer }, "update customer page", "Updated", actor, organization);
    }

    public override IQueryable<Customer> GetQueryable(Organization organization)
    {
        return base.GetQueryable(organization)
            .Include(c => c.Creator)
            .Include(c => c.TagAssignments)
            .ThenInclude(a => a.Tag)
            .Include(c => c.Rooms)
            .Include(c => c.Metadata)
            .ThenInclude(m => m.MetadataField);
    }

    public async Task AssignRoomAsync(Room room, Id<Customer>? customerId, Member actor)
    {
        var customer = customerId is null
            ? null
            : await Db.Customers.SingleOrDefaultAsync(c =>
                c.Id == customerId && c.OrganizationId == actor.OrganizationId);

        room.Customer = customer;
        await Db.SaveChangesAsync();

        var auditEvent = new AuditEventBuilder
        {
            Type = new AuditEventType("Customer", "AssignedToRoom"),
            Description = customer is not null
                ? $"Assigned customer {customer.Name} to room {room.Name}."
                : $"Unassigned customer for room {room.Name}.",
            Actor = actor,
            Organization = actor.Organization,
        };
        await AuditLog.LogAuditEventAsync(auditEvent);
    }

    public async Task<CustomerTag> CreateCustomerSegmentAsync(
        string name,
        Member actor,
        Organization organization)
    {
        var results = await GetOrCreateSegmentsByNamesAsync(new[] { name }, actor, organization);
        return results.Single();
    }

    IQueryable<CustomerTag> GetCustomerSegmentQueryable(Id<Organization> organizationId)
        => Db.CustomerTags
            .Include(ct => ct.Creator)
            .Include(ct => ct.Assignments)
            .ThenInclude(a => a.Customer)
            .Where(t => t.OrganizationId == organizationId);

    public async Task<CustomerTag?> GetCustomerSegmentByIdAsync(
        Id<CustomerTag> id,
        Organization organization)
        => await GetCustomerSegmentQueryable(organization).SingleOrDefaultAsync(t => t.Id == id);

    public async Task<IReadOnlyList<CustomerTag>> GetAllCustomerSegmentsAsync(Id<Organization> organizationId)
        => await GetCustomerSegmentQueryable(organizationId).ToListAsync();

    public async Task<IPaginatedList<CustomerTag>> GetAllCustomerSegmentsAsync(
        Id<Organization> organizationId,
        int pageNumber,
        int pageSize)
    {
        return await PaginatedList.CreateAsync(
            GetCustomerSegmentQueryable(organizationId),
            pageNumber,
            pageSize);
    }

    public async Task AssignCustomerToSegmentsAsync(
        Customer customer,
        IEnumerable<Id<CustomerTag>> customerSegmentIds,
        Member actor)
    {
        var ids = customerSegmentIds.Select(id => id.Value).ToList();
        var segmentsToRemove = customer.TagAssignments
            .Where(t => !ids.Contains(t.TagId))
            .ToList();
        foreach (var segmentToRemove in segmentsToRemove)
        {
            customer.TagAssignments.Remove(segmentToRemove);
        }

        var existingSegmentIds = customer.TagAssignments.Select(t => t.TagId).ToArray();
        var segmentIdsToAdd = ids.Where(id => !existingSegmentIds.Contains(id));
        var segmentsToAdd = await Db.CustomerTags
            .Where(t => t.OrganizationId == customer.OrganizationId)
            .Where(t => segmentIdsToAdd.Contains(t.Id))
            .ToListAsync();
        foreach (var tagToAdd in segmentsToAdd)
        {
            customer.TagAssignments.Add(new CustomerTagAssignment
            {
                Customer = customer,
                CustomerId = customer.Id,
                Tag = tagToAdd,
                TagId = tagToAdd.Id,
                Created = _clock.UtcNow,
                Creator = actor.User,
                ModifiedBy = actor.User,
                Modified = _clock.UtcNow,
            });
        }
        await Db.SaveChangesAsync();
    }

    public async Task AssignCustomerToRoomsAsync(Customer customer, IEnumerable<string> roomIds)
    {
        var ids = roomIds.ToList();
        var roomsToRemove = customer.Rooms
            .Where(r => !ids.Contains(r.PlatformRoomId))
            .ToList();
        foreach (var roomToRemove in roomsToRemove)
        {
            customer.Rooms.Remove(roomToRemove);
        }

        var existingRoomIds = customer.Rooms.Select(r => r.PlatformRoomId).ToArray();
        var roomIdsToAdd = ids.Where(id => !existingRoomIds.Contains(id));
        var roomsToAdd = await Db.Rooms
            .Where(r => r.OrganizationId == customer.OrganizationId)
            .Where(r => roomIdsToAdd.Contains(r.PlatformRoomId))
            .ToListAsync();

        customer.Rooms.AddRange(roomsToAdd);
        await Db.SaveChangesAsync();
    }

    async Task LogActivityAsync(
        IEnumerable<Customer> customers,
        string source,
        string verb,
        Member actor,
        Organization organization,
        string? description = null)
        => await LogActivityAsync(customers.Select(c => c.ToCustomerInfo()).ToList(),
            source,
            verb,
            actor,
            organization,
            description);

    async Task LogActivityAsync(
        IReadOnlyCollection<CustomerInfo> customers,
        string source,
        string verb,
        Member actor,
        Organization organization,
        string? description = null)
    {
        if (customers.Count is 0)
        {
            return;
        }
        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Customer", verb),
                Actor = actor,
                Organization = organization,
                Description = description ?? $"{verb} {customers.Count.ToQuantity("customer")}.",
                Properties = new CustomerEventProperties(customers),
            });
        _analyticsClient.Track(
            $"Customer {verb}",
            AnalyticsFeature.Customers,
            actor,
            organization,
            new {
                count = customers.Count,
                source,
            });
    }

    public async Task RemoveCustomerSegmentAsync(CustomerTag tag, Member actor)
    {
        Db.CustomerTags.Remove(tag);
        await Db.SaveChangesAsync();
        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("CustomerSegment", "Deleted"),
                Actor = actor,
                Organization = tag.Organization,
                Description = $"Deleted customer segment {tag.Name}.",
            });
    }

    public async Task<EntityResult> RemoveCustomerAsync(Customer customer, Member actor, Organization organization)
    {
        if (await CanRemoveCustomerAsync(customer) is { IsSuccess: false } cannotResult)
        {
            return cannotResult;
        }

        Db.Customers.Remove(customer);
        await Db.SaveChangesAsync();
        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Customer", "Deleted"),
                Actor = actor,
                Organization = organization,
                Description = $"Deleted customer {customer.Name}.",
            });

        return EntityResult.Success();
    }

    /// <summary>
    /// Returns <see cref="EntityResult.Success()"/> confirming the <see cref="Customer"/> can be deleted,
    /// or an <see cref="EntityResult.ErrorMessage"/> explaining why not.
    /// </summary>
    /// <param name="id">The <see cref="Customer.Id"/>.</param>
    public async Task<EntityResult> CanRemoveCustomerAsync(Id<Customer> id)
    {
        if (await Db.PlaybookRuns.AnyAsync(pr => pr.Related!.CustomerId == id))
        {
            return EntityResult.Conflict("Cannot delete Customer that has Playbook Runs.");
        }

        return EntityResult.Success();
    }

    public async Task<CustomerCounts> GetCustomerCountsAsync(Organization organization)
    {
        var total = await GetQueryable(organization).CountAsync();
        var withoutRooms = await GetQueryable(organization)
            .Where(o => !o.Rooms.Any())
            .CountAsync();
        return new CustomerCounts(total, withoutRooms);
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersByMetadataValueAsync(Organization organization, string metadataFieldName, string metadataValue)
    {
        // Find the field.
        var field = await Db.MetadataFields
            .SingleOrDefaultAsync(f => f.OrganizationId == organization.Id && f.Name == metadataFieldName && f.Type == MetadataFieldType.Customer);
        if (field is null)
        {
            return Array.Empty<Customer>();
        }

        // Find customers that match our query
        var customers = await GetQueryable(organization)
            .Where(c => c.Metadata.Any(f => f.MetadataFieldId == field.Id && f.Value == metadataValue))
            .ToListAsync();
        return customers;
    }
}

public record CustomerCounts(int Total, int WithoutRooms);

public record CustomerEventProperties(IReadOnlyCollection<CustomerInfo> Tasks);
