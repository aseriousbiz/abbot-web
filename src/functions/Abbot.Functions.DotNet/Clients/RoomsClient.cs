using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NodaTime;
using Serious.Abbot.Functions.Models;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Functions.Execution;

public class RoomsClient : IRoomsClient
{
    static readonly ILogger<RoomsClient> Log = ApplicationLoggerFactory.CreateLogger<RoomsClient>();

    readonly ISkillApiClient _apiClient;
    readonly ISkillContextAccessor _skillContextAccessor;

    /// <summary>
    /// Constructs a <see cref="RoomsClient"/>.
    /// </summary>
    /// <param name="apiClient">The <see cref="ISkillApiClient"/> used to call skill runner APIs.</param>
    /// <param name="skillContextAccessor"></param>
    public RoomsClient(ISkillApiClient apiClient, ISkillContextAccessor skillContextAccessor)
    {
        _apiClient = apiClient;
        _skillContextAccessor = skillContextAccessor;
    }

    public async Task<IResult<IRoomInfo>> CreateAsync(string name, bool isPrivate)
    {
        Log.CreateRoom(name, isPrivate, RoomsApiUrl);

        var response = await _apiClient.SendJsonAsync<object, ConversationInfoResponse>(
            RoomsApiUrl,
            HttpMethod.Put,
            new { name, is_private = isPrivate });

        return response is { Ok: true, Body: { } }
            ? new Result<IRoomInfo>(new RoomInfo(response.Body.Id, response.Body.Name, response.Body.Topic?.Value ?? string.Empty, response.Body.Purpose?.Value ?? string.Empty))
            : new Result<IRoomInfo>(response?.Error ?? "unknown error occurred");
    }

    public async Task<IResult> ArchiveAsync(IRoomMessageTarget room)
    {
        var url = GetRoomUrl(room).Append("/archive");
        Log.ChangeRoomProperty("Archive Status", "archived", room.Id, url);
        var response = await _apiClient.SendAsync<ApiResponse>(
            url,
            HttpMethod.Put);
        return response is { Ok: true }
            ? new Result()
            : new Result(response?.Error ?? "unknown error occurred");
    }

    public async Task<IResult> InviteUsersAsync(IRoomMessageTarget room, IEnumerable<IChatUser> users)
    {
        var userIds = users.Select(u => u.Id).ToList();
        var url = GetRoomUrl(room);
        Log.InviteUsersToRoom(userIds, room.Id, url);

        var response = await _apiClient.SendJsonAsync<object, ApiResponse>(
            url,
            HttpMethod.Post,
            userIds);
        return response is { Ok: true }
            ? new Result()
            : new Result(response?.Error ?? "unknown error occurred");
    }

    public async Task<IResult> SetTopicAsync(IRoomMessageTarget room, string topic)
    {
        var url = GetRoomUrl(room).Append("/topic");
        Log.ChangeRoomProperty("Topic", topic, room.Id, url);
        var response = await _apiClient.SendJsonAsync<object, ApiResponse>(
            url,
            HttpMethod.Post,
            topic);
        return response is { Ok: true }
            ? new Result()
            : new Result(response?.Error ?? "unknown error occurred");
    }

    public async Task<IResult> SetPurposeAsync(IRoomMessageTarget room, string purpose)
    {
        var url = GetRoomUrl(room).Append("/purpose");
        Log.ChangeRoomProperty("Purpose", purpose, room.Id, url);
        var response = await _apiClient.SendJsonAsync<object, ApiResponse>(
            url,
            HttpMethod.Post,
            purpose);
        return response is { Ok: true }
            ? new Result()
            : new Result(response?.Error ?? "unknown error occurred");
    }

    public async Task<AbbotResponse> UpdateMetadataAsync(IRoomMessageTarget room, IReadOnlyDictionary<string, string?> metadata)
    {
        var url = GetRoomUrl(room).Append("/metadata");
        return await _apiClient.SendApiAsync<RoomMetadataUpdate, AbbotResponse>(
            url,
            HttpMethod.Patch,
            new RoomMetadataUpdate(metadata));
    }

    public IRoomMessageTarget GetTarget(string id) => new RoomMessageTarget(id);

    public async Task<IResult<IRoomDetails>> GetDetailsAsync(IRoomMessageTarget room)
    {
        var url = GetRoomUrl(room).Append("/details");
        var result = await _apiClient.GetAsync<RoomDetails>(url);
        return result is null
            ? new Result<IRoomDetails>($"unknown error occurred retrieving room details for {room.Id}.")
            : new Result<IRoomDetails>(result);
    }

    public async Task<IResult<IReadOnlyList<WorkingHours>>> GetCoverageAsync(
        IRoomMessageTarget room,
        RoomRole roomRole,
        string? timeZoneId)
    {
        if (timeZoneId is not null && DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId) is null)
        {
            return new Result<IReadOnlyList<WorkingHours>>($"{timeZoneId} is not a valid or supported time zone.");
        }
        var tz = timeZoneId
            ?? _skillContextAccessor.SkillContext?.SkillInfo.From.TimeZone?.Id;

        var url = GetRoomUrl(room)
            .Append("/coverage")
            .Append(roomRole.ToString())
            .AppendQueryString("tz", tz);
        var result = await _apiClient.GetAsync<IReadOnlyList<WorkingHours>>(url);
        return result is null
            ? new Result<IReadOnlyList<WorkingHours>>("unknown error occurred")
            : new Result<IReadOnlyList<WorkingHours>>(result);
    }

    public async Task<AbbotResponse> NotifyAsync(RoomNotification notification, IRoomMessageTarget room)
    {
        var url = GetRoomUrl(room).Append("/notification");
        var result = await _apiClient.SendApiAsync<object, RoomNotification>(url, HttpMethod.Post, notification);
        return result;
    }

    Uri RoomsApiUrl => _apiClient.BaseApiUrl.Append("/rooms");

    Uri GetRoomUrl(IRoomMessageTarget room)
    {
        return RoomsApiUrl.AppendEscaped(room.Id);
    }
}
