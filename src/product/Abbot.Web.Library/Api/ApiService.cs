using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.PublicApi.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Filters;

namespace Serious.Abbot.Api;

public class ApiService
{
    readonly AbbotContext _db;
    readonly IUserRepository _userRepository;
    readonly IRoomRepository _roomRepository;

    public ApiService(AbbotContext db, IUserRepository userRepository, IRoomRepository roomRepository)
    {
        _db = db;
        _userRepository = userRepository;
        _roomRepository = roomRepository;
    }

    public async Task<OrganizationDetails> GetOrganizationDetailsAsync(Organization organization)
    {
        var counts = await _db.Organizations
            .Where(o => o.Id == organization.Id)
            .Select(o => new {
                SkillCount = o.Skills.Count,
                MemberCount = o.Members.Count,
                AgentCount = o.Members.Count(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Agent)),
                AdminCount = o.Members.Count(m => m.MemberRoles.Any(r => r.Role.Name == Roles.Administrator)),
                IntegrationCount = o.Integrations.Count
            })
            .SingleAsync();

        var defaultFirstResponders = (await _userRepository.GetDefaultFirstRespondersAsync(organization))
            .Select(MemberIdentifier.FromEntity);
        var defaultEscalationResponders = (await _userRepository.GetDefaultEscalationRespondersAsync(organization))
            .Select(MemberIdentifier.FromEntity);
        return OrganizationDetails.FromEntity(
            organization,
            new OrganizationCounts(counts.MemberCount, counts.AgentCount, counts.AdminCount, counts.SkillCount),
            defaultFirstResponders,
            defaultEscalationResponders);
    }

    /// <summary>
    /// Retrieves a list of rooms for the current org.
    /// </summary>
    public async Task<IReadOnlyList<IRoom>> GetAllRoomsAsync(
        Organization organization,
        FilterList filter = default,
        TrackStateFilter trackedStateFilter = TrackStateFilter.Tracked,
        int page = 0,
        int pageSize = 100)
    {
        var rooms = await _roomRepository.GetPersistentRoomsAsync(
            organization,
            filter,
            trackedStateFilter,
            page,
            pageSize);
        return rooms.Select(r => r.ToPlatformRoom()).ToReadOnlyList();
    }

    /// <summary>
    /// Retrieves detailed information about a single room.
    /// </summary>
    /// <param name="id">The platform-specific room id. In Slack, this is the channel.</param>
    /// <param name="organization">The organization.</param>
    public async Task<RoomDetailsResponse?> GetRoomDetailsAsync(string id, Organization organization)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(id, organization);
        if (room is null)
            return null;

        var conversationCount = await _db.Conversations.CountAsync(c => c.RoomId == room.Id);
        return RoomDetailsResponse.FromEntity(room, conversationCount);
    }
}
