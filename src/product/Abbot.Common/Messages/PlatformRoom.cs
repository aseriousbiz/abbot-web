using Newtonsoft.Json;
using Serious.Abbot.Scripting;
using Serious.Text;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents a chat room within Abbot.
/// </summary>
/// <param name="Id">The platform specific identifier for the room (aka <c>channel</c> in Slack).</param>
/// <param name="Name">The name of the room, if known.</param>
public record PlatformRoom(string Id, string? Name) : IRoom
{
    /// <summary>
    /// Gets the <see cref="ChatAddress"/> that can be used in the Reply API to send a message to this room.
    /// </summary>
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public ChatAddress Address => new(ChatAddressType.Room, Id);

    /// <summary>
    /// Provide a nice formatted string to display in Activity Logs etc.
    /// </summary>
    public override string ToString() => $"<#{Id}>";

    /// <summary>
    /// Gets a string suitable for use in audit logs.
    /// </summary>
    public string ToAuditLogString()
    {
        var name = Name is { Length: > 0 }
            ? $"{Name.EnsurePrefix('#').ToMarkdownInlineCode()}"
            : "_a channel with an unknown name_";
        return $"{name} ({Id.ToMarkdownInlineCode()})";
    }
}
