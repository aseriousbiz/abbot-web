using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Models;

/// <summary>
/// The view model for the Insights/_Rooms partial which renders conversation volume by room.
/// </summary>
/// <param name="Rooms">The set of <see cref="InsightsRoom"/> instances to render.</param>
/// <param name="TotalOpenConversationCount">The total number of open conversations across all rooms that match the filter.</param>
public record InsightRoomConversationVolumeViewModel(IReadOnlyList<InsightsRoom> Rooms, int TotalOpenConversationCount)
{
    public static InsightRoomConversationVolumeViewModel FromRoomVolumes(
        IEnumerable<RoomConversationVolume> roomVolumes,
        int totalConversationCount)
    {
        var insightRooms = roomVolumes
            .Select(r => InsightsRoom.FromRoom(r.Room, r.OpenConversationCount, totalConversationCount))
            .ToList();

        return new InsightRoomConversationVolumeViewModel(insightRooms, totalConversationCount);
    }
}

/// <summary>
/// Represents the conversation volume for a specified room.
/// </summary>
/// <param name="Name">The name of the room</param>
/// <param name="PlatformRoomId">The platform-specific Id of the room.</param>
/// <param name="FirstResponders">The list of first-responders for the room with information to render their avatars.</param>
/// <param name="PercentOfTotal">The percent (as an integer from 0 - 100) of the total open conversations in this period represented by this room.</param>
/// <param name="Count">The number of conversations.</param>
/// <param name="CustomerName">The name of the associated customer, if any.</param>
public record InsightsRoom(
    string Name,
    string PlatformRoomId,
    AvatarStackViewModel FirstResponders,
    int PercentOfTotal,
    int Count,
    string? CustomerName)
{
    /// <summary>
    /// Retrieves an instance of <see cref="InsightsRoom"/> from a <see cref="Room"/>. The room needs to include
    /// the <see cref="Organization"/>, <see cref="RoomAssignment"/>s, and room assignment <see cref="Member"/>s.
    /// </summary>
    /// <param name="room">The room.</param>
    /// <param name="openConversationsCount">The count of conversations for the room.</param>
    /// <param name="totalOpenConversationCount">The total number of open conversations.</param>
    /// <returns></returns>
    public static InsightsRoom FromRoom(Room room, int openConversationsCount, int totalOpenConversationCount)
    {
        var firstResponders = room.GetFirstResponders();
        var avatarStack = AvatarStackViewModel.FromMembers(
            firstResponders,
            room.Organization,
            u => $"@{u.DisplayName} is a first responder");
        var percent = totalOpenConversationCount > 0
            ? (int)Math.Round(100 * (openConversationsCount / (double)totalOpenConversationCount))
            : 0;

        return new InsightsRoom(
            room.Name ?? "(unknown)",
            room.PlatformRoomId,
            avatarStack,
            percent,
            openConversationsCount,
            room.Customer?.Name);
    }
}

/// <summary>
/// The view model for the Insights/_Responders partial which renders conversation volume by room.
/// </summary>
/// <param name="Responders">The set of <see cref="InsightsRoom"/> instances to render.</param>
/// <param name="TotalOpenConversationCount">The total number of open conversations across all rooms that match the filter.</param>
public record InsightResponderConversationVolumeViewModel(
    IReadOnlyList<InsightsResponder> Responders,
    int TotalOpenConversationCount)
{
    public static InsightResponderConversationVolumeViewModel FromResponderVolumes(IReadOnlyList<ResponderConversationVolume> responderVolumes)
    {
        var totalConversationsCount = responderVolumes.Sum(v => v.OpenConversationCount);

        var insightsResponders = responderVolumes
            .Select(r => InsightsResponder.FromMember(r.Member, r.OpenConversationCount, totalConversationsCount))
            .ToList();

        return new InsightResponderConversationVolumeViewModel(insightsResponders, totalConversationsCount);
    }
}

/// <summary>
/// Represents the conversation volume for a specified responder.
/// </summary>
/// <param name="Name">The name of the room</param>
/// <param name="Avatar">The responder's Avatar.</param>
/// <param name="AssignedFirstResponderRooms">The names of the rooms where this responder is a first-responder.</param>
/// <param name="PercentOfTotal">The percent (as an integer from 0 - 100) of the total open conversations in this period represented by this room.</param>
/// <param name="Count">The number of conversations.</param>
public record InsightsResponder(
    string Name,
    Avatar Avatar,
    IReadOnlyList<string> AssignedFirstResponderRooms,
    int PercentOfTotal,
    int Count)
{
    /// <summary>
    /// Retrieves an instance of <see cref="InsightsRoom"/> from a <see cref="Room"/>. The room needs to include
    /// the <see cref="Organization"/>, <see cref="RoomAssignment"/>s, and room assignment <see cref="Member"/>s.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="openConversationsCount">The count of conversations for the room.</param>
    /// <param name="totalOpenConversationCount">The total number of open conversations.</param>
    public static InsightsResponder FromMember(Member member, int openConversationsCount, int totalOpenConversationCount)
    {
        var percent = totalOpenConversationCount > 0
            ? (int)Math.Round(100 * (openConversationsCount / (double)totalOpenConversationCount))
            : 0;

        var firstResponderRooms = member
            .RoomAssignments
            .Where(a => a.Role == RoomRole.FirstResponder)
            .Select(a => "#" + (a.Room.Name ?? "unknown"))
            .ToReadOnlyList();
        return new InsightsResponder(member.User.DisplayName,
            Avatar.FromMember(member),
            firstResponderRooms,
            percent,
            openConversationsCount);
    }
}
