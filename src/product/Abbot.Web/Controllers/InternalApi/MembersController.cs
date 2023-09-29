using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Internal API for Conversation management
/// </summary>
[Route("api/internal/members")]
public class MembersController : InternalApiControllerBase
{
    const int MaxLimit = 100;

    readonly IUserRepository _userRepository;

    public MembersController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Gets all active users.
    /// </summary>
    /// <response code="200">A list of members matching the provided query.</response>
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<MemberResponseModel>>> GetActiveUsersAsync()
    {
        var users = await _userRepository.GetActiveMembersQueryable(Organization).ToListAsync();

        return Ok(users.Select(r => MemberResponseModel.Create(r, CurrentMember)).ToList());
    }

    /// <summary>
    /// Gets all members used to populate a type-ahead query.
    /// </summary>
    /// <response code="200">A list of members matching the provided query.</response>
    [HttpGet("typeahead")]
    [ProducesResponseType(typeof(IReadOnlyList<TypeAheadResponseModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TypeAheadResponseModel>>> GetUsersAsync(
        [FromQuery] string? q,
        [FromQuery] int limit = 10)
    {
        var members = await _userRepository.GetMembersForTypeAheadQueryAsync(
            Organization,
            q,
            limit);

        return Ok(members.Select(TypeAheadResponseModel.Create).ToList());
    }

    /// <summary>
    /// Finds a list of users matching the provided query.
    /// </summary>
    /// <param name="q">A string to search for.</param>
    /// <param name="limit">The maximum number of results to return. Values above the internal maximum limit will be disregarded.</param>
    /// <param name="role">The role to filter the users by.</param>
    /// <response code="200">A list of members matching the provided query.</response>
    [HttpGet("find")]
    [ProducesResponseType(typeof(IReadOnlyList<MemberResponseModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FindUserAsync(
        [FromQuery] string? q = null,
        [FromQuery] int limit = MaxLimit,
        [FromQuery] string? role = null)
    {
        limit = Math.Clamp(limit, limit, MaxLimit);

        bool startsWithAtSign = q is { Length: > 0 } && q[0] == '@';
        q = startsWithAtSign
            ? q![1..]
            : q;

        var matches = await _userRepository.FindMembersAsync(Organization, q, limit, role);

        return Ok(matches.Select(m => MemberResponseModel.Create(m, CurrentMember, startsWithAtSign))
            .ToList());
    }
}
