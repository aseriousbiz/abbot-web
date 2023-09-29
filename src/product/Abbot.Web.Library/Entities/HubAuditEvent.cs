using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

public class HubAuditEvent : LegacyAuditEvent
{
    [NotMapped]
    public override bool HasDetails => true;
}

public class HubAuditEventProperties
{
    /// <summary>
    /// If specified, the ID of the <see cref="HubRoutingRule"/> that was modified in this event.
    /// </summary>
    public int? RoutingRuleId { get; set; }

    /// <summary>
    /// If specified, the ID of the source <see cref="Room"/> involved in the event.
    /// For example, if the event is the creation of a routing rule,
    /// this value will contain the ID of the room that the rule was created for.
    /// </summary>
    public int? SourceRoomId { get; set; }

    /// <summary>
    /// If specified, the emoji involved in the event.
    /// For example, if the event is the creation of a routing rule,
    /// this value will contain the emoji that the rule is triggered by.
    /// </summary>
    public string? Emoji { get; set; }

    public Dictionary<string, object?> ToAnalyticsProperties(Member actor)
    {
        var dict = new Dictionary<string, object?>
        {
            { "staff", actor.IsStaff() },
        };

        if (Emoji is { Length: > 0 })
        {
            dict["emoji"] = Emoji;
        }

        return dict;
    }
}
