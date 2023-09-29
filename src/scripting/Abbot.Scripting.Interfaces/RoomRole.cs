using System.Runtime.Serialization;

namespace Serious.Abbot.Scripting;

/// <summary>
/// The roles a user can be assigned to in a room.
/// </summary>
public enum RoomRole
{
    /// <summary>
    /// A first responder receives notifications for messages from supportees that are about to hit their
    /// warning threshold or are overdue.
    /// </summary>
    [EnumMember(Value = "first-responder")]
    FirstResponder,

    /// <summary>
    /// An escalation responder receives notifications for messages from supportees that are overdue.
    /// </summary>
    [EnumMember(Value = "escalation-responder")]
    EscalationResponder,
}
