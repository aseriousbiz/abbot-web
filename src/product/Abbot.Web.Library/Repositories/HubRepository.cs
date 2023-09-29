using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Repositories;

public class HubRepository : IHubRepository
{
    readonly AbbotContext _db;
    readonly IAuditLog _auditLog;
    readonly IClock _clock;

    public HubRepository(AbbotContext db, IAuditLog auditLog, IClock clock)
    {
        _db = db;
        _auditLog = auditLog;
        _clock = clock;
    }

    IQueryable<Hub> Entities => _db.Hubs
        .Include(h => h.Room.Organization);

    public Task<Hub?> GetHubAsync(Room room) =>
        Entities.FirstOrDefaultAsync(h => h.OrganizationId == room.OrganizationId && h.RoomId == room.Id);

    public Task<Hub?> GetHubByIdAsync(Id<Hub> hubId) =>
        Entities.FirstOrDefaultAsync(h => h.Id == hubId.Value);

    public async Task<Hub> CreateHubAsync(string name, Room room, Member actor)
    {
        var hub = new Hub()
        {
            Name = name,
            Room = room,
            RoomId = room.Id,
            OrganizationId = room.OrganizationId,
            Created = _clock.UtcNow,
        };

        await _db.Hubs.AddAsync(hub);
        await _db.SaveChangesAsync();

        await _auditLog.LogHubEventAsync(
            actor,
            hub,
            "Hub created",
            $"Created hub `{hub.Name}` in `#{room.Name}`");

        return hub;
    }

    public async Task<IReadOnlyList<Hub>> GetAllHubsAsync(Organization organization) =>
        await Entities.Where(h => h.OrganizationId == organization.Id).ToListAsync();

    public async Task<Hub?> GetDefaultHubAsync(Organization organization) =>
        organization.Settings?.DefaultHubId is { } hubId
        ? await GetHubByIdAsync(hubId) : null;

    public async Task<Hub?> SetDefaultHubAsync(Hub hub, Member actor)
    {
        var prevHub = await GetDefaultHubAsync(hub.Organization);
        if (prevHub?.Id == hub.Id)
        {
            return hub;
        }

        hub.Organization.Settings =
            (hub.Organization.Settings ?? new()) with
            {
                DefaultHubId = hub,
            };
        await _db.SaveChangesAsync();

        await _auditLog.LogHubEventAsync(
            actor,
            hub,
            "Set default hub",
            prevHub == null
                ? $"Organization default Hub set to `{hub.Name}`"
                : $"Organization default Hub changed from `{prevHub.Name}` to `{hub.Name}`");

        return prevHub;
    }

    public async Task<Hub?> ClearDefaultHubAsync(Organization organization, Member actor)
    {
        var hub = await GetDefaultHubAsync(organization);

        if (organization.Settings is { DefaultHubId: not null } settings)
        {
            organization.Settings = settings with
            {
                DefaultHubId = null,
            };
            await _db.SaveChangesAsync();
        }

        if (hub is null)
        {
            return null;
        }

        await _auditLog.LogHubEventAsync(
            actor,
            hub,
            "Clear default hub",
            $"Hub `{hub.Name}` is no longer the Organization default Hub");

        return hub;
    }

    public async Task DeleteHubAsync(Hub hub, Member actor)
    {
        _db.Hubs.Remove(hub);
        await _db.SaveChangesAsync();

        await _auditLog.LogHubEventAsync(
            actor,
            hub,
            "Hub deleted",
            $"Deleted hub `{hub.Name}`");
    }

    public async Task<IReadOnlyList<Room>> GetAttachedRoomsAsync(Hub hub)
    {
        if (!hub.AttachedRooms.IsLoaded)
        {
            await _db.Entry(hub).Collection(o => o.AttachedRooms).LoadAsync();
        }

        return hub.AttachedRooms;
    }
}
