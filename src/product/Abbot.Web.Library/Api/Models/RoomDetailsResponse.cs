using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.PublicApi.Models;


/// <summary>
/// The response to a request for a room details.
/// </summary>
public record RoomDetailsResponse
{
    public required IRoom Identifier { get; init; }

    public required Threshold<TimeSpan> ResponseTimes { get; init; }

    public required IReadOnlyList<RoomMetadataValue> Metadata { get; init; }

    public required IReadOnlyList<MemberIdentifier> FirstResponders { get; init; }

    public required IReadOnlyList<MemberIdentifier> EscalationResponders { get; init; }

    public required bool ManagedConversationsEnabled { get; init; }

    public required DateTime CreatedUtc { get; init; }

    public required HubDetails? Hub { get; init; }

    public required int ConversationCount { get; init; }

    public static RoomDetailsResponse FromEntity(Room entity, int conversationCount) => new()
    {
        Identifier = entity.ToPlatformRoom(),
        FirstResponders = entity.Assignments
            .Where(a => a.Role is RoomRole.FirstResponder)
            .Select(a => MemberIdentifier.FromEntity(a.Member))
            .ToList(),
        EscalationResponders = entity.Assignments
            .Where(a => a.Role is RoomRole.EscalationResponder)
            .Select(a => MemberIdentifier.FromEntity(a.Member))
            .ToList(),
        ResponseTimes = entity.TimeToRespond,
        Metadata = entity.Metadata.Select(m => new RoomMetadataValue(m.MetadataField.Name, m.Value)).ToList(),
        ManagedConversationsEnabled = entity.ManagedConversationsEnabled,
        CreatedUtc = entity.Created,
        Hub = HubDetails.FromEntity(entity.Hub),
        ConversationCount = conversationCount,
    };
}
