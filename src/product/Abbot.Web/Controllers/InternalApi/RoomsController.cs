using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Internal API for Conversation management
/// </summary>
[Route("api/internal/rooms")]
public class RoomsController : InternalApiControllerBase
{
    const int MaxLimit = 100;

    readonly IRoomRepository _roomRepository;

    public RoomsController(IRoomRepository roomRepository)
    {
        _roomRepository = roomRepository;
    }

    /// <summary>
    /// Gets all tracked rooms.
    /// </summary>
    /// <response code="200">A list of members matching the provided query.</response>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomResponseModel>>> GetRoomsAsync()
    {
        var rooms = await _roomRepository.GetPersistentRoomsAsync(
            Organization,
            default,
            TrackStateFilter.BotIsMember,
            1,
            int.MaxValue);

        return Ok(rooms.Select(r => RoomResponseModel.Create(r, CurrentMember)).ToList());
    }

    /// <summary>
    /// Gets all tracked rooms used to populate a type-ahead query.
    /// </summary>
    /// <response code="200">A list of members matching the provided query.</response>
    [HttpGet("typeahead")]
    [ProducesResponseType(typeof(IReadOnlyList<TypeAheadResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TypeAheadResponseModel>>> GetRoomsAsync(
        [FromQuery] string? q,
        [FromQuery] string? currentPlatformRoomId,
        [FromQuery] int limit = 10)
    {
        var rooms = await _roomRepository.GetRoomsForTypeAheadQueryAsync(
            Organization,
            q,
            currentPlatformRoomId,
            limit);

        return Ok(rooms.Select(TypeAheadResponseModel.Create).ToList());
    }

    /// <summary>
    /// Finds a list of rooms matching the provided query.
    /// </summary>
    /// <param name="q">A string to search for.</param>
    /// <param name="limit">The maximum number of results to return. Values above the internal maximum limit will be disregarded.</param>
    /// <response code="200">A list of members matching the provided query.</response>
    [HttpGet("find")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomResponseModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FindRoomAsync([FromQuery] string? q = null, [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, limit, MaxLimit);

        var matches = await _roomRepository.FindRoomsAsync(Organization, q, limit);

        return Ok(matches.Select(r => RoomResponseModel.Create(r, CurrentMember)).ToList());
    }
}
