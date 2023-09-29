using Serious.Abbot.Entities;

namespace Serious.Abbot.Models.Api;

public record RoomResponseModel(
    int Id,
    bool? IsLocal,
    string PlatformRoomId,
    string Name,
    Uri? PlatformUrl,
    bool Persistent,
    bool ManagedConversationsEnabled)
{
    public static RoomResponseModel Create(Room room, Member? viewer = null) =>
        new(
            room.Id,
            viewer is null ? null : room.OrganizationId == viewer.OrganizationId,
            room.PlatformRoomId,
            room.Name ?? string.Empty,
            room.GetLaunchUrl(),
            room.Persistent,
            room.ManagedConversationsEnabled);
}
