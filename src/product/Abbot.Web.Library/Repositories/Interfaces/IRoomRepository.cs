using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Serious.Abbot.Entities;
using Serious.Collections;
using Serious.Filters;

namespace Serious.Abbot.Repositories;

/// <summary>
/// The repository for rooms.
/// </summary>
public interface IRoomRepository
{
    /// <summary>
    /// Create a room
    /// </summary>
    /// <param name="room">The room to create.</param>
    Task<Room> CreateAsync(Room room);

    /// <summary>
    /// Deletes the room.
    /// </summary>
    /// <param name="room">The room to delete.</param>
    Task RemoveAsync(Room room);

    /// <summary>
    /// Updates the room.
    /// </summary>
    /// <param name="room">The room to update.</param>
    Task UpdateAsync(Room room);

    /// <summary>
    /// Gets a <see cref="Room"/> using the platform-specific room ID, and the organization containing the room.
    /// </summary>
    /// <param name="platformRoomId">The platform-specific room ID (Channel ID in Slack).</param>
    /// <param name="organization">The organization containing the room.</param>
    /// <returns>A <see cref="Room"/> matching the room id, or <c>null</c> if no such room exists.</returns>
    Task<Room?> GetRoomByPlatformRoomIdAsync(string platformRoomId, Organization organization);

    /// <summary>
    /// Gets a <see cref="Room"/> using the room name, and the organization containing the room.
    /// </summary>
    /// <param name="roomName">The name of the room.</param>
    /// <param name="organization">The organization containing the room.</param>
    /// <returns>A <see cref="Room"/> matching the room name, or <c>null</c> if no such room exists.</returns>
    Task<Room?> GetRoomByNameAsync(string roomName, Organization organization);

    /// <summary>
    /// Retrieves a set of <see cref="Room"/> instances using the platform-specific room IDs, and the organization
    /// containing the rooms.
    /// </summary>
    /// <param name="platformRoomIds">The platform-specific room IDs (Channel IDs in Slack).</param>
    /// <param name="organization">The organization containing the room.</param>
    /// <returns>A <see cref="Room"/> matching the room id, or <c>null</c> if no such room exists.</returns>
    Task<IReadOnlyList<RoomLookupResult>> GetRoomsByPlatformRoomIdsAsync(IEnumerable<string> platformRoomIds, Organization organization);

    /// <summary>
    /// Assigns <paramref name="member"/> to the <paramref name="role"/> in <paramref name="room"/>.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> in which to make the assignment.</param>
    /// <param name="member">The <see cref="Member"/> to assign.</param>
    /// <param name="role">The role (from <see cref="RoomRole"/>) to assign to the <paramref name="member" />.</param>
    /// <param name="actor">The <see cref="User"/> who is performing the action, for audit logging.</param>
    /// <returns><c>true</c> if the assignment was made, <c>false</c> if the <paramref name="member"/> is already assigned to <paramref name="role"/> in <paramref name="room"/></returns>
    Task<bool> AssignMemberAsync(Room room, Member member, RoomRole role, Member actor);

    /// <summary>
    /// Assigns the set of users specified by <paramref name="platformUserIds"/> to the the <paramref name="role"/> in
    /// <paramref name="room"/> and removes any other assignments in the room for the same role.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> in which to make the assignment.</param>
    /// <param name="platformUserIds">The set of users to assign to this role.</param>
    /// <param name="role">The role (from <see cref="RoomRole"/>) to assign to the users specified <paramref name="platformUserIds" />.</param>
    /// <param name="actor">The <see cref="Member"/> who is performing the action, for audit logging.</param>
    Task SetRoomAssignmentsAsync(Room room, IEnumerable<string> platformUserIds, RoomRole role, Member actor);

    /// <summary>
    /// Replaces the <see cref="Member"/>s assigned to the room <paramref name="role"/> for the <paramref name="room"/>
    /// with the members specified by <paramref name="memberIds"/>. If none of the members can be added (for example,
    /// none of them are agents), then the room role will not be cleared.
    /// </summary>
    /// <remarks>This is used for the Staff import tool.</remarks>
    /// <param name="room">The <see cref="Room"/> in which to make the assignment.</param>
    /// <param name="memberIds">The set of database Ids for the <see cref="Member"/>s to assign to this role.</param>
    /// <param name="role">The role (from <see cref="RoomRole"/>) to assign to the users specified <paramref name="memberIds" />.</param>
    /// <param name="actor">The <see cref="User"/> who is performing the action, for audit logging.</param>
    Task ReplaceRoomAssignmentsAsync(Room room, IEnumerable<Id<Member>> memberIds, RoomRole role, Member actor);

    /// <summary>
    /// Removes the assignment of <paramref name="member"/> to <paramref name="role"/> in <paramref name="room"/>.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> in which to remove the assignment.</param>
    /// <param name="member">The <see cref="Member"/> to remove.</param>
    /// <param name="role">The role (from <see cref="RoomRole"/>) to remove from the <paramref name="member" />.</param>
    /// <param name="actor">The <see cref="User"/> who is performing the action, for audit logging.</param>
    /// <returns><c>true</c> if the assignment was removed, <c>false</c> if the <paramref name="member"/> was not assigned to <paramref name="role"/> in <paramref name="room"/></returns>
    Task<bool> UnassignMemberAsync(Room room, Member member, RoomRole role, Member actor);

    /// <summary>
    /// Gets a list of all the rooms where managed conversations are enabled.
    /// </summary>
    /// <param name="organization">The organization to fetch rooms for</param>
    /// <param name="filter">Returns rooms where the name contains the filter, or the room's platform Id matches the filter.</param>
    /// <param name="trackedStateFilter">Used to filter on the tracking state (managed conversations enabled) of the room.</param>
    /// <param name="page">The 1-based page index.</param>
    /// <param name="pageSize">The number of rooms to return for the specified <paramref name="page"/>.</param>
    /// <returns>A list of all persistent rooms with managed conversations enabled in the organization.</returns>
    Task<IPaginatedList<Room>> GetPersistentRoomsAsync(
        Organization organization,
        FilterList filter,
        TrackStateFilter trackedStateFilter,
        int page,
        int pageSize);

    /// <summary>
    /// Gets a list of tracked rooms that match the type-ahead query. This query always returns the "current" room if
    /// the current `PlatformRoomId` is specified.
    /// </summary>
    /// <param name="organization">The organization to fetch rooms for</param>
    /// <param name="roomNameFilter">The name to search for.</param>
    /// <param name="currentPlatformRoomId">The room to always include in results.</param>
    /// <param name="limit">The number of results to return.</param>
    /// <returns>A list of <see cref="Room"/>s that match the room name filter.</returns>
    Task<IReadOnlyList<Room>> GetRoomsForTypeAheadQueryAsync(
        Organization organization,
        string? roomNameFilter,
        string? currentPlatformRoomId,
        int limit);

    /// <summary>
    /// Returns the count of all the rooms that match the filters where managed conversations are enabled and
    /// not enabled.
    /// </summary>
    /// <param name="organization">The organization to fetch rooms for</param>
    /// <param name="filter">The filter to apply, if any.</param>
    /// <returns>A <see cref="RoomCountsResult"/> with the count of tracked and untracked rooms.</returns>
    Task<RoomCountsResult> GetPersistentRoomCountsAsync(Organization organization, FilterList filter);

    /// <summary>
    /// Retrieves all rooms that match the specified customer segments.
    /// </summary>
    /// <param name="segments">The database IDs of the customer segments.</param>
    /// <param name="trackStateFilter">A filter to apply to the rooms to retrieve.</param>
    /// <param name="organization">The organization to query.</param>
    /// <returns>A collection of rooms belonging to customers in the specified customer segments.</returns>
    Task<IReadOnlyList<Room>> GetRoomsByCustomerSegmentsAsync(
        IEnumerable<Id<CustomerTag>> segments,
        TrackStateFilter trackStateFilter,
        Organization organization);

    /// <summary>
    /// Gets a paged list of all the rooms where managed conversations are enabled.
    /// </summary>
    /// <param name="organization">The organization to fetch rooms for</param>
    /// <param name="filter">A filter to apply.</param>
    /// <param name="page">The 1-based page of data to return.</param>
    /// <param name="pageSize">The size of the page.</param>
    /// <returns>A list of all persistent rooms with managed conversations enabled in the organization.</returns>
    Task<IPaginatedList<Room>> GetConversationRoomsAsync(
        Organization organization,
        FilterList filter,
        int page,
        int pageSize);

    /// <summary>
    /// Gets a room by its ID.
    /// </summary>
    /// <param name="roomId">The ID of the <see cref="Room"/> to retrieve.</param>
    /// <returns>A <see cref="Room"/> representing the requested room, or <c>null</c> if the room does not exist.</returns>
    Task<Room?> GetRoomAsync(Id<Room>? roomId);

    /// <summary>
    /// Finds rooms by a loose substring match on their name.
    /// </summary>
    /// <param name="organization">The organization the rooms belong to.</param>
    /// <param name="nameQuery">The name to search for.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    Task<IReadOnlyList<Room>> FindRoomsAsync(Organization organization, string? nameQuery, int limit);

    /// <summary>
    /// Enables or disables conversations tracking.
    /// </summary>
    /// <param name="room">The room to enable or disable conversation tracking for.</param>
    /// <param name="enabled">Whether or not conversation tracking is enabled.</param>
    /// <param name="actor">The user that made the change.</param>
    Task SetConversationManagementEnabledAsync(Room room, bool enabled, Member actor);

    /// <summary>
    /// Gets a boolean indicating if there are any rooms with Abbot as a member.
    /// </summary>
    Task<bool> HasPersistentRoomWithAbbotAsync(Organization organization);

    /// <summary>
    /// Removes all room assignments for the member. This is typically done when removing the user from all roles.
    /// If the room assignments aren't loaded, this will load them first.
    /// </summary>
    /// <param name="member">The member to remove roles for.</param>
    Task RemoveAllRoomAssignmentsForMemberAsync(Member member);

    /// <summary>
    /// Retrieves all the room assignments for the member.
    /// </summary>
    /// <param name="member">The member to remove roles for.</param>
    Task<IReadOnlyList<RoomAssignment>> GetRoomAssignmentsAsync(Member member);

    /// <summary>
    /// Creates a new <see cref="RoomLink"/> to link the provided conversation with an external resource.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> to be linked.</param>
    /// <param name="type">The <see cref="RoomLinkType"/> representing the type of the link.</param>
    /// <param name="externalId">The external ID of the resource being linked.</param>
    /// <param name="displayName">The display name the resource being linked.</param>
    /// <param name="actor">The <see cref="Member"/> who is creating the link.</param>
    /// <param name="utcTimestamp">The UTC timestamp at which the link was created.</param>
    Task CreateLinkAsync(Room room, RoomLinkType type, string externalId, string displayName, Member actor,
        DateTime utcTimestamp);

    /// <summary>
    /// Removes the provided <see cref="RoomLink"/>.
    /// </summary>
    /// <param name="link">The <see cref="RoomLink"/> to remove</param>
    /// <param name="actor">The <see cref="Member"/> who is removing the link.</param>
    Task RemoveLinkAsync(RoomLink link, Member actor);

    /// <summary>
    /// Updates the target response times for this room.
    /// </summary>
    /// <param name="room">The room to update.</param>
    /// <param name="target">The warning threshold.</param>
    /// <param name="deadline">The deadline threshold.</param>
    /// <param name="actor">The user setting the response times.</param>
    /// <returns><c>true</c> if a change was made, otherwise <c>false</c>.</returns>
    Task<bool> UpdateResponseTimesAsync(Room room, TimeSpan? target, TimeSpan? deadline, Member actor);

    /// <summary>
    /// Attaches the specified <see cref="Room"/> to the specified <see cref="Hub"/>
    /// </summary>
    /// <param name="room">The <see cref="Room"/> to attach to the <see cref="Hub"/>.</param>
    /// <param name="hub">The <see cref="Hub"/> to attach the <see cref="Room"/> to.</param>
    /// <param name="actor">The <see cref="Member"/> who performed this action.</param>
    /// <returns>An <see cref="EntityResult"/> describing the outcome of the operation.</returns>
    Task<EntityResult> AttachToHubAsync(Room room, Hub hub, Member actor);

    /// <summary>
    /// Detaches the specified <see cref="Room"/> from the specified <see cref="Hub"/>
    /// </summary>
    /// <param name="room">The <see cref="Room"/> to detach from the <see cref="Hub"/>.</param>
    /// <param name="hub">The <see cref="Hub"/> to detach the <see cref="Room"/> from.</param>
    /// <param name="actor">The <see cref="Member"/> who performed this action.</param>
    /// <returns>An <see cref="EntityResult"/> describing the outcome of the operation.</returns>
    Task<EntityResult> DetachFromHubAsync(Room room, Hub hub, Member actor);

    /// <summary>
    /// Updates the last activity timestamp for the room. Activity is defined as any message event associated
    /// with the room.
    /// </summary>
    /// <remarks>
    /// We may want to
    /// </remarks>
    /// <param name="room"></param>
    /// <returns></returns>
    Task UpdateLastMessageActivityAsync(Room room);
}

/// <summary>
/// When doing a batch lookup of rooms, this contains the result for each room.
/// </summary>
/// <param name="PlatformRoomId">The platform specific Id of the room we were looking for.</param>
/// <param name="Exists">Whether it exists or not.</param>
/// <param name="Room">If it exists, the <see cref="Room"/> record we have.</param>
public record RoomLookupResult(
    string PlatformRoomId,
    [property: MemberNotNullWhen(true, "Room")]
    bool Exists,
    Room? Room);

/// <summary>
/// When looking up the counts for rooms, the count of rooms with managed conversations
/// enabled, and the count of rooms with managed conversations not enabled.
/// </summary>
/// <param name="Tracked">The count of rooms with managed conversations enabled.</param>
/// <param name="Untracked">The count of rooms with managed conversations disabled.</param>
/// <param name="BotMissing">The count of rooms with Abbot missing.</param>
/// <param name="Inactive">The count of rooms that are inactive (deleted or archived).</param>
public record RoomCountsResult(
    RoomCount Tracked,
    RoomCount Untracked,
    RoomCount Hubs,
    RoomCount BotMissing,
    RoomCount Inactive);

/// <summary>
/// A room count.
/// </summary>
/// <param name="TotalCount">The total count.</param>
/// <param name="FilteredCount">The active or filtered count.</param>
public record struct RoomCount(int TotalCount, int? FilteredCount = null);

/// <summary>
/// Used to filter queries on whether a room is tracked (managed conversation enabled).
/// </summary>
[Flags]
public enum TrackStateFilter
{
    // All persistent rooms.
#pragma warning disable CA1008
    All = 0,
#pragma warning restore CA1008

    /// <summary>
    /// Hubs
    /// </summary>
    Hubs = 0b0000_0001,

    /// <summary>
    /// Rooms with conversation tracking enabled.
    /// </summary>
    Tracked = 0b0000_0010,

    /// <summary>
    /// Rooms that do not have conversation tracking enabled.
    /// </summary>
    Untracked = 0b0000_0100,

    /// <summary>
    /// Rooms that are archived or deleted.
    /// </summary>
    Inactive = 0b0000_1000,

    /// <summary>
    /// Rooms that Abbot knows of, but have Abbot missing.
    /// </summary>
    BotMissing = 0b0001_0000,

    /// <summary>
    /// Active rooms with Abbot present.
    /// </summary>
    BotIsMember = Hubs | Tracked | Untracked,
}
