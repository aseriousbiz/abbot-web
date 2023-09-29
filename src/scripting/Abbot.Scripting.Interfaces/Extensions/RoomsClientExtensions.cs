using System.Collections.Generic;
using System.Threading.Tasks;
using NodaTime;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Useful extensions to <see cref="IRoomsClient"/>
/// </summary>
public static class RoomsClientExtensions
{
    /// <summary>
    /// Creates a public Room and returns the created Id of the room.
    /// </summary>
    /// <param name="client">The <see cref="IRoomsClient"/> this extends.</param>
    /// <param name="name">The name of the room.</param>
    /// <returns>The ID of the created room.</returns>
    public static Task<IResult<IRoomInfo>> CreateAsync(this IRoomsClient client, string name)
    {
        return client.CreateAsync(name, false);
    }

    /// <summary>
    /// Retrieves the coverage for a room in the caller's timezone.
    /// This is the set of working hours that responders are available.
    /// </summary>
    /// <param name="client">The <see cref="IRoomsClient"/> this extends.</param>
    /// <param name="room">The room to retrieve information about.</param>
    /// <param name="roomRole">The type of room role to check for coverage such as first responders or escalation responders.</param>
    /// <returns>An <see cref="IRoomDetails"/> with detailed room information. Returns <c>null</c> if the room is not found.</returns>
    public static async Task<IResult<IReadOnlyList<WorkingHours>>> GetCoverageAsync(
        this IRoomsClient client,
        IRoomMessageTarget room,
        RoomRole roomRole) => await client.GetCoverageAsync(room, roomRole, null);


    /// <summary>
    /// Retrieves the coverage for a room in the specified timezone.
    /// This is the set of working hours that responders are available.
    /// </summary>
    /// <param name="client">The <see cref="IRoomsClient"/> this extends.</param>
    /// <param name="room">The room to retrieve information about.</param>
    /// <param name="roomRole">The type of room role to check for coverage such as first responders or escalation responders.</param>
    /// <param name="timeZone">The time zone to get coverage for.</param>
    /// <returns>An <see cref="IRoomDetails"/> with detailed room information. Returns <c>null</c> if the room is not found.</returns>
    public static async Task<IResult<IReadOnlyList<WorkingHours>>> GetCoverageAsync(
        this IRoomsClient client,
        IRoomMessageTarget room,
        RoomRole roomRole,
        DateTimeZone timeZone) => await client.GetCoverageAsync(room, roomRole, timeZone.Id);
}
