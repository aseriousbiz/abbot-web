using System.Collections.Generic;
using System.Linq;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NodaTime;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Controllers;

/// <summary>
/// This provides services to the skill runners to manage conversations. At the moment, it only supports
/// Slack.
/// </summary>
public class RoomsController : SkillRunnerApiControllerBase
{
    static readonly ILogger<RoomsController> Log = ApplicationLoggerFactory.CreateLogger<RoomsController>();

    readonly IConversationsApiClient _apiClient;
    readonly IRoomRepository _roomRepository;
    readonly IUserRepository _userRepository;
    readonly IMetadataRepository _metadataRepository;
    readonly IPublishEndpoint _publishEndpoint;
    readonly IClock _clock;

    public RoomsController(
        IConversationsApiClient apiClient,
        IRoomRepository roomRepository,
        IUserRepository userRepository,
        IMetadataRepository metadataRepository,
        IPublishEndpoint publishEndpoint,
        IClock clock)
    {
        _apiClient = apiClient;
        _roomRepository = roomRepository;
        _userRepository = userRepository;
        _metadataRepository = metadataRepository;
        _publishEndpoint = publishEndpoint;
        _clock = clock;
    }

    /// <summary>
    /// Retrieve information about a room.
    /// </summary>
    /// <param name="roomId">The id of the room to get information for.</param>
    [HttpGet("rooms/{roomId}")]
    public async Task<IActionResult> GetAsync(string roomId)
    {
        if (!Skill.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return Unauthorized();
        }

        var response = await _apiClient.GetConversationInfoAsync(apiToken, roomId);
        return response.Ok
            ? new ObjectResult(response.Body)
            : Problem(response.Error, Request.Path);
    }

    /// <summary>
    /// Create a room.
    /// </summary>
    /// <param name="createRequest">Information about the room to create.</param>
    [HttpPut("rooms")]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ConversationCreateRequest createRequest)
    {
        if (!Skill.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return Unauthorized();
        }

        var response = await _apiClient.CreateConversationAsync(apiToken, createRequest);
        return new ObjectResult(response);
    }

    /// <summary>
    /// Archives a room.
    /// </summary>
    /// <param name="roomId">The Id of the room to archive.</param>
    [HttpPut("rooms/{roomId}/archive")]
    public async Task<IActionResult> ArchiveAsync(
        string roomId)
    {
        if (!Skill.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return Unauthorized();
        }

        var response = await _apiClient.ArchiveConversationAsync(apiToken, roomId);
        return new ObjectResult(response);
    }

    /// <summary>
    /// Invite users to a room.
    /// </summary>
    /// <param name="roomId">The Id of the room to invite the users to.</param>
    /// <param name="userIds">The Ids of the users to invite.</param>
    [HttpPost("rooms/{roomId}")]
    public async Task<IActionResult> InviteAsync(
        string roomId,
        [FromBody] IEnumerable<string> userIds)
    {
        if (!Skill.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return Unauthorized();
        }

        var inviteRequest = new UsersInviteRequest(roomId, userIds);
        var response = await _apiClient.InviteUsersToConversationAsync(apiToken, inviteRequest);
        return new ObjectResult(response);
    }

    /// <summary>
    /// Sets the room's topic
    /// </summary>
    /// <param name="roomId">The Id of the room to invite the users to.</param>
    /// <param name="topic">The topic to set.</param>
    [HttpPost("rooms/{roomId}/topic")]
    public async Task<IActionResult> SetTopicAsync(
        string roomId,
        [FromBody] string topic)
    {
        if (!Skill.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return Unauthorized();
        }

        var topicRequest = new TopicRequest(roomId, topic);
        var response = await _apiClient.SetConversationTopicAsync(apiToken, topicRequest);
        return new ObjectResult(response);
    }

    /// <summary>
    /// Sets the room's purpose
    /// </summary>
    /// <param name="roomId">The Id of the room to invite the users to.</param>
    /// <param name="purpose">The purpose to set.</param>
    [HttpPost("rooms/{roomId}/purpose")]
    public async Task<IActionResult> SetPurposeAsync(
        string roomId,
        [FromBody] string purpose)
    {
        if (!Skill.Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            Log.OrganizationHasNoSlackApiToken();
            return Unauthorized();
        }

        var purposeRequest = new PurposeRequest(roomId, purpose);
        var response = await _apiClient.SetConversationPurposeAsync(apiToken, purposeRequest);
        return new ObjectResult(response);
    }

    /// <summary>
    /// Updates metadata for the room.
    /// </summary>
    /// <param name="roomId">The Id of the room to invite the users to.</param>
    /// <param name="update">The metadata to update.</param>
    [HttpPatch("rooms/{roomId}/metadata")]
    public async Task<IActionResult> PatchMetadataAsync(string roomId, [FromBody] RoomMetadataUpdate update)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }

        await _metadataRepository.UpdateRoomMetadataAsync(
            room,
            update.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Member);

        return new ObjectResult(AbbotResponse.Success(200));
    }

    [HttpGet("rooms/{roomid}/details")]
    public async Task<IActionResult> GetDetailsAsync(string roomId)
    {
        var organization = Skill.Organization;

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, organization);
        if (room is null)
        {
            return NotFound();
        }
        using var roomScope = Log.BeginRoomScope(room);

        var firstResponders = room.GetFirstResponders().Select(r => r.ToPlatformUser()).ToList();
        var escalationResponders = room.GetEscalationResponders().Select(r => r.ToPlatformUser()).ToList();
        var metadata = room.Metadata.ToDictionary(m => m.MetadataField.Name, m => m.Value ?? string.Empty);
        var defaultFirstResponders = (await _userRepository.GetDefaultFirstRespondersAsync(organization))
            .Select(r => r.ToPlatformUser())
            .ToList();
        var defaultEscalationResponders = (await _userRepository.GetDefaultEscalationRespondersAsync(organization))
            .Select(r => r.ToPlatformUser())
            .ToList();

        var roomDetails = new RoomDetails(
            room.PlatformRoomId,
            room.Name,
            room.BotIsMember,
            room.ManagedConversationsEnabled,
            room.Archived is true,
            room.Customer?.ToCustomerInfo(),
            new ResponseSettings(
                room.TimeToRespond,
                firstResponders,
                escalationResponders),
            new ResponseSettings(
                Skill.Organization.DefaultTimeToRespond,
                defaultFirstResponders,
                defaultEscalationResponders),
            metadata);
        return new ObjectResult(roomDetails);
    }

    /// <summary>
    /// Endpoint that returns the coverage for a room in the specified timezone. If no timezone is provided, attempts
    /// to use the timezone of the caller. If the caller timezone is not known, falls back to "UTC".
    /// </summary>
    /// <param name="roomId">The channel.</param>
    /// <param name="roomRole">The responders role to calculate coverage for.</param>
    /// <param name="tz">The timezone to calculate coverage in.</param>
    [HttpGet("rooms/{roomid}/coverage/{roomRole}")]
    public async Task<IActionResult> GetCoverageAsync(string roomId, RoomRole roomRole, string? tz)
    {
        var organization = Skill.Organization;

        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, organization);
        if (room is null)
        {
            return NotFound();
        }
        using var roomScope = Log.BeginRoomScope(room);

        // TODO: Look up member. Unfortunately we don't pass the member id so it's a bit of a pain to get the member.
        // Doable, but a bigger change than I want to make right now.
        tz ??= "UTC";

        var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(tz);
        if (timeZone is null)
        {
            return BadRequest("Invalid Timezone"); // This gets logged by by the caller.
        }

        IReadOnlyList<Member> responders;
        if (roomRole is RoomRole.FirstResponder)
        {
            responders = room.GetFirstResponders().ToList();
            if (!responders.Any())
            {
                responders = await _userRepository.GetDefaultFirstRespondersAsync(organization);
            }
        }
        else
        {
            responders = room.GetEscalationResponders().ToList();
            if (!responders.Any())
            {
                responders = await _userRepository.GetDefaultEscalationRespondersAsync(organization);
            }
        }

        var model = responders.CalculateCoverage(timeZone, WorkingHours.Default, _clock.UtcNow);
        return new ObjectResult(model);
    }

    /// <summary>
    /// Sends a notification to the room's responders. If the room is attached to a Hub, then the message is sent to
    /// the hub and the responders are mentioned. Otherwise the message is sent as a group DM to the responders.
    /// </summary>
    /// <param name="roomId">The Id of the room to invite the users to.</param>
    /// <param name="notification">The notification to send.</param>
    [HttpPost("rooms/{roomId}/notification")]
    public async Task<IActionResult> NotifyAsync(
        string roomId,
        [FromBody] RoomNotification notification)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(roomId, Organization);
        if (room is null)
        {
            return NotFound();
        }
        await _publishEndpoint.Publish(new PublishRoomNotification
        {
            OrganizationId = Organization,
            RoomId = room,
            Notification = notification,
        });
        return Ok(new { });
    }
}
