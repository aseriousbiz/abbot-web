using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Api;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;
using Serious.Filters;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Internal API for Conversation management
/// </summary>
[Route("api/internal/conversations")]
public class ConversationsController : InternalApiControllerBase
{
    readonly InsightsApiService _insightsApiService;
    readonly IConversationRepository _conversationRepository;
    readonly IRoomRepository _roomRepository;
    readonly IMessageRenderer _messageRenderer;

    public ConversationsController(
        InsightsApiService insightsApiService,
        IConversationRepository conversationRepository,
        IRoomRepository roomRepository,
        IMessageRenderer messageRenderer)
    {
        _insightsApiService = insightsApiService;
        _conversationRepository = conversationRepository;
        _roomRepository = roomRepository;
        _messageRenderer = messageRenderer;
    }

    /// <summary>
    /// Gets a list of conversations matching the provided criteria.
    /// </summary>
    /// <param name="room">
    /// (Optional) The platform ID of a room.
    /// If set, only conversations in that room will be returned (cannot be specified with <paramref name="role" />).
    /// </param>
    /// <param name="role">
    /// (Optional) A room "role".
    /// If set, only conversations in room where the user is assigned that role will be returned (cannot be specified with <paramref name="room" />).
    /// Can be set multiple times, and will act as an OR.
    /// </param>
    /// <param name="state">(Optional) A state filter. If set, only conversations with this state will be returned.</param>
    /// <param name="page">(Optional) A page number of results to return. If not set, the first page of results is returned.</param>
    /// <response code="200">Returns the list of matching conversations.</response>
    /// <response code="400">The arguments provided were invalid.</response>
    /// <response code="404">The provided room could not be found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ConversationListResponseModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetListAsync(string? room = null, ConversationStateFilter? state = null, [FromQuery] RoomRole[]? role = null, int page = 1)
    {
        var query = new ConversationQuery(Organization.Id);
        if (room is { Length: > 0 })
        {
            if (role is { Length: > 0 })
            {
                return ProblemBadArguments(
                    "Conflicting arguments",
                    "Cannot specify both 'room' and 'role'");
            }

            var resolvedRoom = await _roomRepository.GetRoomByPlatformRoomIdAsync(room, Organization);
            if (resolvedRoom is null)
            {
                return ProblemNotFound(
                    "Room not found",
                    $"Room '{room}' not found, or is not part of this organization.");
            }

            query = query.InRooms(resolvedRoom.Id);
        }
        else if (role is { Length: > 0 })
        {
            query = query.InRoomsWhereAssigned(CurrentMember.Id, role);
        }

        if (state is { } s)
        {
            query = query.WithState(s);
        }

        return await GetConversationResponse(query, page);
    }

    /// <summary>
    /// Gets the authenticated user's conversation queue.
    /// </summary>
    /// <param name="page">(Optional) A page number of results to return. If not set, the first page of results is returned.</param>
    /// <response code="200">Returns the list of conversations in the user's queue.</response>
    /// <response code="400">The arguments provided were invalid.</response>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(ConversationListResponseModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueueAsync(int page = 1)
    {
        var query = ConversationQuery.QueueFor(CurrentMember);
        return await GetConversationResponse(query, page);
    }

    /// <summary>
    /// Gets conversation trends information for the current user.
    /// </summary>
    /// <param name="range">The date range to return data for.</param>
    /// <param name="tz">
    /// The <see href="https://www.iana.org/time-zones">IANA time zone name</see> to use to build the report.
    /// </param>
    /// <param name="filter">Options for filtering the data. <see cref="InsightsRoomFilter" />.</param>
    /// <param name="tag">The tag to filter on.</param>
    /// <param name="q">The filter to use for customers, segments, etc.</param>
    /// <response code="200">Returns the conversation trends information.</response>
    /// <response code="400">The arguments provided were invalid.</response>
    [HttpGet("trends")]
    [ProducesResponseType(typeof(TrendsResponseModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrendsAsync(
        DateRangeOption? range = null,
        string? tz = null,
        string? filter = null,
        string? tag = null,
        FilterList q = default)
    {
        var trends = await _insightsApiService.GetTrendsAsync(CurrentMember, range, tz, filter, tag, q);
        return Ok(trends);
    }

    async Task<IActionResult> GetConversationResponse(ConversationQuery query, int page)
    {
        var convos = await _conversationRepository.QueryConversationsWithStatsAsync(query,
            DateTime.UtcNow,
            page,
            WebConstants.LongPageSize);

        var models = new List<ConversationViewModel>();
        foreach (var convo in convos.Conversations)
        {
            var title = await _messageRenderer.RenderMessageAsync(convo.Title, convo.Organization);
            var summary = await _messageRenderer.RenderMessageAsync(convo.Summary, convo.Organization);
            models.Add(new ConversationViewModel(convo, title, summary));
        }

        return Ok(ConversationListResponseModel.Create(models, convos, CurrentMember));
    }
}
