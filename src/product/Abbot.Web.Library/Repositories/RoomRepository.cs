using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Extensions;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using Serious.Filters;
using Serious.Logging;
using Serious.Slack;
using Serious.Tasks;

namespace Serious.Abbot.Repositories;

public sealed class RoomRepository : IRoomRepository
{
    static readonly ILogger<RoomRepository> Log = ApplicationLoggerFactory.CreateLogger<RoomRepository>();

    // Don't update last activity unless it's been three hours since the last update.
    // This throttles back constant updates for active rooms.
    static readonly TimeSpan LastActivityThrottle = TimeSpan.FromHours(3);

    readonly AbbotContext _db;
    readonly IClock _clock;
    readonly ISettingsManager _settingsManager;
    readonly IAuditLog _auditLog;

    /// <summary>
    /// Defines the base query that all other queries extend.
    /// </summary>
    IQueryable<Room> RoomsQuery => _db.Rooms
        .Include(r => r.Customer)
        .ThenInclude(c => c!.TagAssignments)
        .ThenInclude(ta => ta.Tag)
        .Include(r => r.Customer)
        .ThenInclude(c => c!.Metadata)
        .ThenInclude(m => m.MetadataField)
        .Include(r => r.Metadata)
        .ThenInclude(m => m.MetadataField)
        .Include(r => r.Links)
        .Include(r => r.Organization)
        .Include(r => r.Assignments)
        .ThenInclude(a => a.Member.User)
        .Include(r => r.Hub)
        .ThenInclude(h => h!.Room);

    public RoomRepository(
        AbbotContext db,
        IClock clock,
        ISettingsManager settingsManager,
        IAuditLog auditLog)
    {
        _db = db;
        _clock = clock;
        _settingsManager = settingsManager;
        _auditLog = auditLog;
    }

    public async Task<Room> CreateAsync(Room room)
    {
        room.Created = _clock.UtcNow;
        room.Modified = _clock.UtcNow;
        await _db.Rooms.AddAsync(room);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.GetDatabaseError()
            is UniqueConstraintError
        {
            TableName: "Rooms",
            ColumnNames: [nameof(Room.OrganizationId), nameof(Room.PlatformRoomId)]
                or [nameof(Room.PlatformRoomId), nameof(Room.OrganizationId)]
        })
        {
            var existing = await RoomsQuery
                .SingleOrDefaultAsync(
                    u => u.OrganizationId == room.OrganizationId
                    && u.PlatformRoomId == room.PlatformRoomId);

            if (existing is not null)
            {
                Log.RecoveredFromDuplicateRoomException(e, existing.PlatformRoomId, existing.Organization.PlatformId);
                _db.Entry(room).State = EntityState.Detached;
                return existing;
            }

            throw;
        }

        return room;
    }

    public async Task RemoveAsync(Room room)
    {
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Room room)
    {
        room.Modified = _clock.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<Room?> GetRoomByPlatformRoomIdAsync(string platformRoomId, Organization organization)
    {
        // Calling GetRoomsByPlatformRoomIdsAsync instead of implementing this method directly is a bit more
        // inefficient, but benefits from keeping the logic the same.
        var result = await GetRoomsByPlatformRoomIdsAsync(
            Enumerable.Repeat(platformRoomId, 1),
            organization);

        return result.Single().Room;
    }

    public async Task<Room?> GetRoomByNameAsync(string roomName, Organization organization)
    {
        return await RoomsQuery
            .Where(r => r.OrganizationId == organization.Id)
            .Where(r => r.Name == roomName)
            .SingleOrDefaultAsync();
    }

    public async Task<IReadOnlyList<RoomLookupResult>> GetRoomsByPlatformRoomIdsAsync(IEnumerable<string> platformRoomIds, Organization organization)
    {
        var rooms = await RoomsQuery
            .Where(r => r.OrganizationId == organization.Id)
            .Where(r => platformRoomIds.Contains(r.PlatformRoomId))
            .ToDictionaryAsync(r => r.PlatformRoomId);

        RoomLookupResult GatherResult(string platformRoomId)
        {
            var exists = rooms.TryGetValue(platformRoomId, out var room);
            return new RoomLookupResult(platformRoomId, exists, room);
        }

        return platformRoomIds.Select(GatherResult).ToList();
    }

    public async Task<bool> AssignMemberAsync(Room room, Member member, RoomRole role, Member actor)
    {
        var assignments = await GetOrLoadAssignmentsForRoomRoleAsync(room, role);
        if (assignments.Any(a => a.MemberId == member.Id))
        {
            return false;
        }

        if (member.User.IsBot || !member.IsAgent())
        {
            return false;
        }
        await EnqueueAddAssignment(room, member, role, actor.User);
        await _db.SaveChangesAsync();
        await _auditLog.LogRoomRespondersChangedAsync(
            room,
            role,
            new[] { member },
            Array.Empty<Member>(),
            assignments.Count + 1,
            actor);
        return true;
    }

    public async Task SetRoomAssignmentsAsync(Room room, IEnumerable<string> platformUserIds, RoomRole role, Member actor)
    {
        var existingAssignments = await GetOrLoadAssignmentsForRoomRoleAsync(room, role);
        var toRemove = existingAssignments
            .Where(a => !platformUserIds.Contains(a.Member.User.PlatformUserId))
            .ToList();

        foreach (var assignmentToRemove in toRemove)
        {
            _db.RoomAssignments.Remove(assignmentToRemove);
        }

        var toAdd = platformUserIds
            .Except(existingAssignments.Select(a => a.Member.User.PlatformUserId));

        var addedMembers = new List<Member>();
        foreach (var platformUserId in toAdd)
        {
            var member = await _db.Members
                .Include(m => m.User)
                .Where(m => m.OrganizationId == room.OrganizationId)
                .SingleOrDefaultAsync(m => m.User.PlatformUserId == platformUserId);

            if (member is not null && !member.User.IsBot)
            {
                addedMembers.Add(member);
                await EnqueueAddAssignment(room, member, role, actor.User);
            }
        }

        await _db.SaveChangesAsync();
        var updatedAssignments = await GetOrLoadAssignmentsForRoomRoleAsync(room, role);
        await _auditLog.LogRoomRespondersChangedAsync(
            room,
            role,
            addedMembers,
            toRemove.Select(a => a.Member).ToList(),
            updatedAssignments.Count,
            actor);
    }

    public async Task ReplaceRoomAssignmentsAsync(Room room, IEnumerable<Id<Member>> memberIds, RoomRole role, Member actor)
    {
        var memberIdList = memberIds.Select(id => id.Value).ToList();

        var existingAssignments = await GetOrLoadAssignmentsForRoomRoleAsync(room, role);
        var toRemove = existingAssignments
            .Where(a => !memberIdList.Contains(a.Member.Id))
            .ToList();

        var members = await _db.Members
            .Include(m => m.User)
            .Include(m => m.MemberRoles)
            .ThenInclude(m => m.Role)
            .Where(m => m.OrganizationId == room.OrganizationId)
            .Where(m => memberIdList.Contains(m.Id))
            .ToListAsync();

        if (!members.Any())
        {
            // Nothing to replace with, so do nothing.
            return;
        }

        foreach (var assignmentToRemove in toRemove)
        {
            _db.RoomAssignments.Remove(assignmentToRemove);
        }

        var toAdd = memberIdList.Except(existingAssignments.Select(a => a.Member.Id));

        foreach (var memberId in toAdd)
        {
            // This is an iteration per member, but the number of members for any given room is small.
            // So this is probably cheaper than creating a dictionary and then doing a lookup each time.
            var member = members.Single(m => m.Id == memberId);
            await EnqueueAddAssignment(room, member, role, actor.User);
        }

        await _db.SaveChangesAsync();
        var updatedAssignments = await GetOrLoadAssignmentsForRoomRoleAsync(room, role);
        await _auditLog.LogRoomRespondersChangedAsync(
            room,
            role,
            members,
            toRemove.Select(a => a.Member).ToList(),
            updatedAssignments.Count,
            actor);
    }

    async Task EnqueueAddAssignment(Room room, Member member, RoomRole role, User actor)
    {
        var assignment = new RoomAssignment
        {
            Room = room,
            Member = member,
            Role = role,
            Created = _clock.UtcNow,
            Creator = actor,
            Modified = _clock.UtcNow,
            ModifiedBy = actor,
        };
        await _db.RoomAssignments.AddAsync(assignment);
    }

    public async Task<bool> UnassignMemberAsync(Room room, Member member, RoomRole role, Member actor)
    {
        var assignments = await GetOrLoadAssignmentsForRoomRoleAsync(room, role);
        if (assignments.FirstOrDefault(a => a.MemberId == member.Id) is { } matchingAssignment)
        {
            _db.RoomAssignments.Remove(matchingAssignment);
            await _db.SaveChangesAsync();
            await _auditLog.LogRoomRespondersChangedAsync(
                room,
                role,
                Array.Empty<Member>(),
                new[] { member },
                assignments.Count - 1,
                actor);

            return true;
        }

        return false;
    }

    public async Task<IPaginatedList<Room>> GetPersistentRoomsAsync(
        Organization organization,
        FilterList filter,
        TrackStateFilter trackedStateFilter,
        int page,
        int pageSize)
    {
        var query = GetPersistentRoomsQueryable(organization);
        query = ApplyFilter(query, filter);
        query = ApplyTrackedStateFilter(query, trackedStateFilter);

        // When we show inactive rooms, show deleted first, then archived.
        query = trackedStateFilter == TrackStateFilter.Inactive
            ? query
                .OrderBy(r => r.Deleted == true
                    ? -1
                    : r.Archived == true
                        ? 1
                        : 0)
                .ThenBy(r => r.Name)
            : query.OrderBy(r => r.Name);

        return await PaginatedList.CreateAsync(query, page, pageSize);
    }

    public async Task<IReadOnlyList<Room>> GetRoomsForTypeAheadQueryAsync(
        Organization organization,
        string? roomNameFilter,
        string? currentPlatformRoomId,
        int limit)
    {
        var query = ApplyTrackedStateFilter(GetPersistentRoomsQueryable(organization), TrackStateFilter.BotIsMember)
            .Where(r => roomNameFilter == null || EF.Functions.ILike(r.Name!, $"%{roomNameFilter}%"))
            .Union(GetPersistentRoomsQueryable(organization).Where(r => r.PlatformRoomId == currentPlatformRoomId))
            .OrderBy(r => r.Name);

        var limitedQuery = limit > 0
            ? query.Take(limit)
            : query;

        return await limitedQuery.ToListAsync();
    }

    public async Task<RoomCountsResult> GetPersistentRoomCountsAsync(Organization organization, FilterList filter)
    {
        var query = GetPersistentRoomsQueryable(organization);

        var trackedQuery = ApplyTrackedStateFilter(query, TrackStateFilter.Tracked);
        var untrackedQuery = ApplyTrackedStateFilter(query, TrackStateFilter.Untracked);
        var hubsQuery = ApplyTrackedStateFilter(query, TrackStateFilter.Hubs);
        var botMissingQuery = ApplyTrackedStateFilter(query, TrackStateFilter.BotMissing);
        var inactiveQuery = ApplyTrackedStateFilter(query, TrackStateFilter.Inactive);

        var trackedCount = new RoomCount(await trackedQuery.CountAsync());
        var untrackedCount = new RoomCount(await untrackedQuery.CountAsync());
        var hubsCount = new RoomCount(await hubsQuery.CountAsync());
        var botMissingCount = new RoomCount(await botMissingQuery.CountAsync());
        var inactiveCount = new RoomCount(await inactiveQuery.CountAsync());

        if (filter.Any())
        {
            trackedCount = trackedCount with
            {
                FilteredCount = await ApplyFilter(trackedQuery, filter).CountAsync()
            };

            untrackedCount = untrackedCount with
            {
                FilteredCount = await ApplyFilter(untrackedQuery, filter).CountAsync()
            };

            hubsCount = hubsCount with
            {
                FilteredCount = await ApplyFilter(hubsQuery, filter).CountAsync()
            };

            botMissingCount = botMissingCount with
            {
                FilteredCount = await ApplyFilter(botMissingQuery, filter).CountAsync()
            };

            inactiveCount = inactiveCount with
            {
                FilteredCount = await ApplyFilter(inactiveQuery, filter).CountAsync()
            };
        }

        return new RoomCountsResult(trackedCount, untrackedCount, hubsCount, botMissingCount, inactiveCount);
    }

    public async Task<IPaginatedList<Room>> GetConversationRoomsAsync(
        Organization organization,
        FilterList filter,
        int page,
        int pageSize) => await GetPersistentRoomsAsync(
            organization,
            filter,
            TrackStateFilter.Tracked,
            page,
            pageSize);

    public async Task<Room?> GetRoomAsync(Id<Room>? roomId)
    {
        return await RoomsQuery.SingleEntityOrDefaultAsync(roomId);
    }

    public async Task<IReadOnlyList<Room>> FindRoomsAsync(Organization organization, string? nameQuery, int limit)
    {
        var query = GetPersistentRoomsQueryable(organization);

        if (nameQuery is { Length: > 0 })
        {
            nameQuery = nameQuery.ToUpperInvariant();
            // Disabling CA1304 because the ToUpper is actually happening in the database.
#pragma warning disable CA1304
            query = query.Where(r => r.Name != null && r.Name.ToUpper().Contains(nameQuery));
#pragma warning restore CA1304
        }

        query = query.OrderBy(r => r.Name).Take(limit);

        var matches = await query.ToListAsync();

        if (nameQuery is { Length: > 0 })
        {
            // Re-sort by a cheap "relevance" score by just putting those with the match at the start of the name first.
            matches = matches.OrderBy(r => r.Name!.StartsWith(nameQuery, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
                .ThenBy(r => r.Name)
                .ToList();
        }

        return matches;
    }

    public async Task SetConversationManagementEnabledAsync(Room room, bool enabled, Member actor)
    {
        if (room.ManagedConversationsEnabled == enabled)
        {
            return;
        }

        if (enabled)
        {
            // When we enable conversation management, we want to make sure the ReportMissingConversationsJob
            // only looks at messages that occur after managed conversations is enabled for the room.
            // We can manufacture a timestamp and add 5 minutes to avoid race conditions.
            var latestTimestamp = new SlackTimestamp(_clock.UtcNow.AddMinutes(5));
            await _settingsManager.SetLastVerifiedMessageIdAsync(room, latestTimestamp.ToString(), actor.User);
            room.ManagedConversationsEnabled = true;
            // We only care about the last message activity since we started managing conversations for the room.
            room.LastMessageActivityUtc = _clock.UtcNow;
            room.DateManagedConversationsEnabledUtc = _clock.UtcNow;
            await _auditLog.LogManagedConversationsEnabledAsync(actor, room, room.Organization);
        }
        else
        {
            await _settingsManager.SetLastVerifiedMessageIdAsync(room, null, actor.User);
            room.ManagedConversationsEnabled = false;
            await _auditLog.LogManagedConversationsDisabledAsync(actor, room, room.Organization);
        }

        await UpdateAsync(room);
    }

    public async Task<bool> HasPersistentRoomWithAbbotAsync(Organization organization) =>
        await _db.Rooms.Where(r => r.OrganizationId == organization.Id && r.Persistent && r.BotIsMember == true)
            .AnyAsync();

    public async Task RemoveAllRoomAssignmentsForMemberAsync(Member member)
    {
        await EnsureRoomAssignmentsLoadedAsync(member);

        member.RoomAssignments.Clear();
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<RoomAssignment>> GetRoomAssignmentsAsync(Member member)
    {
        await EnsureRoomAssignmentsLoadedAsync(member);

        return member.RoomAssignments.ToList();
    }

    public async Task<IReadOnlyList<Room>> GetRoomsByCustomerSegmentsAsync(
        IEnumerable<Id<CustomerTag>> segments,
        TrackStateFilter trackStateFilter,
        Organization organization)
    {
        var intIds = segments.Select(s => s.Value).ToList();
        var queryable = GetPersistentRoomsQueryable(organization)
            .Where(c => c.Customer!.TagAssignments.Any(ct => intIds.Contains(ct.TagId)));

        queryable = ApplyTrackedStateFilter(queryable, trackStateFilter);
        return await queryable.Distinct().ToReadOnlyListAsync();
    }

    public async Task CreateLinkAsync(Room room, RoomLinkType type, string externalId, string displayName, Member actor, DateTime utcTimestamp)
    {
        var link = new RoomLink()
        {
            Room = room,
            Organization = room.Organization,
            LinkType = type,
            ExternalId = externalId,
            DisplayName = displayName,
            CreatedBy = actor,
            Created = utcTimestamp,
        };

        await _db.RoomLinks.AddAsync(link);
        await _auditLog.LogRoomLinkedAsync(room, type, externalId, displayName, actor, room.Organization);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveLinkAsync(RoomLink link, Member actor)
    {
        _db.RoomLinks.Remove(link);
        await _auditLog.LogRoomUnlinkedAsync(link.Room,
            link.LinkType,
            link.ExternalId,
            link.DisplayName,
            actor,
            link.Organization);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> UpdateResponseTimesAsync(Room room, TimeSpan? target, TimeSpan? deadline, Member actor)
    {
        if (room.TimeToRespond.Warning == target && room.TimeToRespond.Deadline == deadline)
        {
            return false;
        }
        var oldTarget = room.TimeToRespond.Warning;
        var oldDeadline = room.TimeToRespond.Deadline;
        room.TimeToRespond = new Threshold<TimeSpan>(target, deadline);
        await _db.SaveChangesAsync();
        await _auditLog.LogRoomResponseTimesChangedAsync(room, oldTarget, oldDeadline, actor);
        return true;
    }

    public async Task<EntityResult> AttachToHubAsync(Room room, Hub hub, Member actor)
    {
        if (room.HubId == hub.Id)
        {
            return EntityResult.Success();
        }

        if (room.Hub is not null)
        {
            return EntityResult.Conflict("The Room is already attached to a Hub.");
        }

        room.HubId = hub.Id;
        room.Hub = hub;
        await _db.SaveChangesAsync();
        await _auditLog.LogAuditEventAsync(new()
        {
            Type = new("Room", "AttachedToHub"),
            Actor = actor,
            Organization = room.Organization,
            Description = $"Attached #{room.Name} to hub {hub.Name}",
            EntityId = room.Id,
            Properties = new {
                HubId = hub.Id,
            }
        });

        return EntityResult.Success();
    }

    public async Task<EntityResult> DetachFromHubAsync(Room room, Hub hub, Member actor)
    {
        if (room.HubId is null)
        {
            return EntityResult.Success();
        }

        if (room.HubId != hub.Id)
        {
            return EntityResult.Conflict("The Room is not attached to this Hub.");
        }

        room.HubId = null;
        room.Hub = null;
        await _db.SaveChangesAsync();
        await _auditLog.LogAuditEventAsync(new()
        {
            Type = new("Room", "DetachedFromHub"),
            Actor = actor,
            Organization = room.Organization,
            Description = $"Detached #{room.Name} from hub {hub.Name}",
            EntityId = room.Id,
            Properties = new {
                HubId = hub.Id,
            }
        });

        return EntityResult.Success();
    }

    public async Task UpdateLastMessageActivityAsync(Room room)
    {
        // Don't update last message activity unless it's been longer than the throttle interval.
        if (_clock.UtcNow - room.LastMessageActivityUtc > LastActivityThrottle)
        {
            room.LastMessageActivityUtc = _clock.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    async Task EnsureRoomAssignmentsLoadedAsync(Member member)
    {
        // We can't rely on `member.RoomAssignments` being null to determine if the collection is loaded.
        // We have to check `IsLoaded`. If the collection isn't loaded, we're running a query here, which
        // doesn't check `IsLoaded`, hence we keep the `IsLoaded` check here.
        if (!member.RoomAssignments.IsLoaded)
        {
            await _db.Entry(member)
                .Collection(m => m.RoomAssignments)
                .Query()
                .Include(a => a.Room)
                .LoadAsync();
        }
    }

    async Task<IReadOnlyList<RoomAssignment>> GetOrLoadAssignmentsForRoomRoleAsync(Room room, RoomRole roomRole)
    {
        var assignmentsEntry = _db.Entry(room).Collection(r => r.Assignments);
        if (!assignmentsEntry.IsLoaded)
        {
            // Load the assignments if not already loaded
            await assignmentsEntry
                .Query()
                .Include(a => a.Member.Organization)
                .Include(a => a.Member.User)
                .LoadAsync();
        }

        return room.Assignments.Where(a => a.Role == roomRole).ToList();
    }

    IQueryable<Room> GetPersistentRoomsQueryable(Organization organization)
    {
        return RoomsQuery
            .Where(r => r.OrganizationId == organization.Id)
            .Where(r => r.Persistent);
    }

    IQueryable<Room> ApplyTrackedStateFilter(IQueryable<Room> query, TrackStateFilter trackedStateFilter)
        => query.ApplyFilter(new FilterList { Filter.Create("is", $"{trackedStateFilter}") }, _db);

    IQueryable<Room> ApplyFilter(IQueryable<Room> query, FilterList filter)
        => query.ApplyFilter(filter, _db, defaultField: "room");
}

public static partial class RoomRepositoryLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Recovered from duplicate Room error (PlatformRoomId: {PlatformRoomId}, PlatformId: {PlatformId})")]
    public static partial void RecoveredFromDuplicateRoomException(
        this ILogger<RoomRepository> logger,
        Exception exception,
        string platformRoomId,
        string platformId);
}
