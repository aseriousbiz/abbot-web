using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Services;

/// <summary>
/// Used to import data from a CSV file.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Updates the specified room with the set of first responders.
    /// </summary>
    /// <remarks>
    /// If <paramref name="replaceExisting"/> is <c>true</c>, but none of the responder Ids resolve to agents that
    /// can be added as responders, then the room will not be cleared.
    /// </remarks>
    /// <param name="roomId">The database Id for the room to import responders into.</param>
    /// <param name="responderIds">The database Ids for the <see cref="Member"/>s to add as responders.</param>
    /// <param name="roomRole">The <see cref="RoomRole"/> to apply to these responders.</param>
    /// <param name="replaceExisting">When <c>true</c>, the specified responders replace the existing ones.</param>
    /// <param name="actor">The actor that ran the import.</param>
    /// <exception cref="UnreachableException">Thrown if the room Id does not resolve to a room.</exception>
    Task ImportRespondersAsync(
        Id<Room> roomId,
        IEnumerable<Id<Member>> responderIds,
        RoomRole roomRole,
        bool replaceExisting,
        Member actor);
}

public class ImportService : IImportService
{
    readonly IRoomRepository _roomRepository;
    readonly IUserRepository _userRepository;

    public ImportService(IRoomRepository roomRepository, IUserRepository userRepository)
    {
        _roomRepository = roomRepository;
        _userRepository = userRepository;
    }

    public async Task ImportRespondersAsync(
        Id<Room> roomId,
        IEnumerable<Id<Member>> responderIds,
        RoomRole roomRole,
        bool replaceExisting,
        Member actor)
    {
        var room = await _roomRepository.GetRoomAsync(roomId).Require();
        if (replaceExisting)
        {
            await _roomRepository.ReplaceRoomAssignmentsAsync(room, responderIds, roomRole, actor);
        }
        else
        {
            foreach (var responderId in responderIds)
            {
                var member = await _userRepository.GetMemberByIdAsync(responderId, room.Organization);
                if (member is not null)
                {
                    await _roomRepository.AssignMemberAsync(room, member, RoomRole.FirstResponder, actor);
                }
            }
        }
    }
}
