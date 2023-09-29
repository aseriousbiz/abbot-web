using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Api;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Filters;

namespace Serious.Abbot.Controllers.PublicApi;

[ApiController]
[AbbotApiHost]
[Route("api/org")]
[Authorize(Policy = AuthorizationPolicies.PublicApi)]
public class OrganizationController : UserControllerBase
{
    readonly ApiService _api;

    public OrganizationController(ApiService api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var details = await _api.GetOrganizationDetailsAsync(Organization);
        return Json(details);
    }

    /// <summary>
    /// Retrieves a list of rooms for the current org.
    /// </summary>
    /// <returns>A JSON representation of a room.</returns>
    [HttpGet("rooms")]
    public async Task<IActionResult> GetAllRoomsAsync(
        string? filter = null,
        string? responder = null,
        TrackStateFilter trackedStateFilter = TrackStateFilter.Tracked,
        int page = 0,
        int pageSize = 100)
    {
        // TODO: Improve this. We'll just hack this up for now. -@haacked
        var filterList = new FilterList();
        if (filter is not null)
        {
            filterList.Add(Filter.Create("room", filter));
        }
        if (responder is not null)
        {
            filterList.Add(Filter.Create("responder", responder));
        }

        var rooms = await _api.GetAllRoomsAsync(
            Organization,
            filterList,
            trackedStateFilter,
            page,
            pageSize);
        return Json(rooms);
    }

    /// <summary>
    /// Retrieves detailed information about a single room.
    /// </summary>
    /// <param name="id">The platform-specific room id. In Slack, this is the channel.</param>
    /// <returns>A JSON representation of a room.</returns>
    [HttpGet("rooms/{id}")]
    public async Task<IActionResult> GetRoomAsync(string id)
    {
        var room = await _api.GetRoomDetailsAsync(id, Organization);
        return room is null
            ? NotFound()
            : Json(room);
    }
}
