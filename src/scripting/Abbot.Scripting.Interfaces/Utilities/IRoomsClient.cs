using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to manage Slack conversations.
/// </summary>
public interface IRoomsClient
{
    /// <summary>
    /// Creates a Room and returns the created Id of the room.
    /// </summary>
    /// <param name="name">The name of the room.</param>
    /// <param name="isPrivate">Whether or not the room is private.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not and contains information about the created room.</returns>
    Task<IResult<IRoomInfo>> CreateAsync(string name, bool isPrivate);

    /// <summary>
    /// Archives a room.
    /// </summary>
    /// <param name="room">The room to archive.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    Task<IResult> ArchiveAsync(IRoomMessageTarget room);

    /// <summary>
    /// Invites the set of users to the room.
    /// </summary>
    /// <param name="room">The room to invite users to.</param>
    /// <param name="users">The set of users to invite.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    Task<IResult> InviteUsersAsync(IRoomMessageTarget room, IEnumerable<IChatUser> users);

    /// <summary>
    /// Sets the room's topic.
    /// </summary>
    /// <param name="room">The room for which to set a topic.</param>
    /// <param name="topic">The topic to set for the room.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    Task<IResult> SetTopicAsync(IRoomMessageTarget room, string topic);

    /// <summary>
    /// Sets the room's purpose.
    /// </summary>
    /// <param name="room">The room for which to set a purpose.</param>
    /// <param name="purpose">The purpose to set for the room.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    Task<IResult> SetPurposeAsync(IRoomMessageTarget room, string purpose);

    /// <summary>
    /// Sets the room's metadata to match the provided dictionary.
    /// </summary>
    /// <param name="room">The room for which to set metadata.</param>
    /// <param name="metadata">The metadata to set for the room.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    Task<AbbotResponse> UpdateMetadataAsync(IRoomMessageTarget room, IReadOnlyDictionary<string, string?> metadata);

    /// <summary>
    /// Retrieves a handle that can be used to send messages to a room given it's platform-specific ID (for example, the Channel ID 'Cnnnnnnn' in Slack).
    /// </summary>
    /// <remarks>
    /// This method does not confirm that the room exists.
    /// If the room does not exist, sending a message to it will fail silently.
    /// </remarks>
    /// <param name="id">The ID of the room to retrieve</param>
    /// <returns>An <see cref="IRoomMessageTarget"/>, suitable for use in <see cref="MessageOptions.To"/>, referring to the room.</returns>
    IRoomMessageTarget GetTarget(string id);

    /// <summary>
    /// Retrieves detailed information about a room. You can pass <see cref="IBot.Room"/> to this for the current room.
    /// For a different room, call <see cref="GetTarget(string)"/> with the Id of the room (aka channel) to get a handle
    /// to the room.
    /// </summary>
    /// <param name="room">The room to retrieve information about.</param>
    /// <returns>An <see cref="IRoomDetails"/> with detailed room information. Returns <c>null</c> if the room is not found.</returns>
    Task<IResult<IRoomDetails>> GetDetailsAsync(IRoomMessageTarget room);

    /// <summary>
    /// Retrieves the coverage for a room. This is the set of working hours that responders are available.
    /// </summary>
    /// <param name="room">The room to retrieve information about.</param>
    /// <param name="roomRole">The type of room role to check for coverage such as first responders or escalation responders.</param>
    /// <param name="timeZoneId">The IANA time zone Id to get coverage for. If omitted, the caller's timezone is used.</param>
    /// <returns>An <see cref="IRoomDetails"/> with detailed room information. Returns <c>null</c> if the room is not found.</returns>
    Task<IResult<IReadOnlyList<WorkingHours>>> GetCoverageAsync(
        IRoomMessageTarget room,
        RoomRole roomRole,
        string? timeZoneId);

    /// <summary>
    /// Sends a notification to the room's responders. If the room is attached to a Hub, then the message is sent to
    /// the hub and the responders are mentioned. Otherwise the message is sent as a group DM to the responders.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="room">The room to send a notification for.</param>
    Task<AbbotResponse> NotifyAsync(RoomNotification notification, IRoomMessageTarget room);
}
