using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Models;

/// <summary>
/// Information about a chat room (Slack only at this time).
/// </summary>
/// <param name="Id">The ID of the room.</param>
/// <param name="Name">The name of the room</param>
/// <param name="Topic">The topic of the chat room.</param>
/// <param name="Purpose">The purpose of the chat room.</param>
public record RoomInfo(string Id, string? Name, string Topic, string Purpose) : PlatformRoom(Id, Name), IRoomInfo;
