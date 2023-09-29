using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.PublicApi.Models;

public record OrganizationIdentifier(string Id, string? Name)
{
    public static OrganizationIdentifier FromEntity(Organization entity) => new(entity.PlatformId, entity.Name);
}

public record OrganizationDetails
{
    public required OrganizationIdentifier Identifier { get; init; }

    public required DateTime CreatedUtc { get; init; }

    public required Permissions Permissions { get; init; }

    public required int PurchasedSeatCount { get; init; }

    public required string? Scopes { get; init; }

    public required Threshold<TimeSpan> DefaultTimeToRespond { get; init; }

    public required IReadOnlyList<MemberIdentifier> DefaultFirstResponders { get; init; }

    public required IReadOnlyList<MemberIdentifier> DefaultEscalationResponders { get; init; }

    public required OrganizationCounts Counts { get; init; }

    public required string Plan { get; init; }

    public static OrganizationDetails FromEntity(
        Organization entity,
        OrganizationCounts organizationCounts,
        IEnumerable<MemberIdentifier> defaultFirstResponders,
        IEnumerable<MemberIdentifier> defaultEscalationResponders) => new()
        {
            Identifier = OrganizationIdentifier.FromEntity(entity),
            Permissions = new Permissions(entity.ApiEnabled, entity.UserSkillsEnabled, entity.AutoApproveUsers),
            Scopes = entity.Scopes,
            PurchasedSeatCount = entity.PurchasedSeatCount,
            DefaultTimeToRespond = entity.DefaultTimeToRespond,
            DefaultFirstResponders = defaultFirstResponders.ToList(),
            DefaultEscalationResponders = defaultEscalationResponders.ToList(),
            Counts = organizationCounts,
            CreatedUtc = entity.Created,
            Plan = entity.PlanType.GetDisplayName(),
        };
}

#pragma warning disable CA1724
public record Permissions(bool ApiEnabled, bool UserSkillsEnabled, bool AutoApproveUsers);
#pragma warning restore CA1724

public record OrganizationCounts(int MemberCount, int AgentCount, int AdminCount, int SkillCount);

/// <summary>
/// The
/// </summary>
/// <param name="Id">The platform-specific Id of the member.</param>
/// <param name="Name">The name of the member.</param>
public record MemberIdentifier(string Id, string Name)
{
    /// <summary>
    /// Creates an instance of <see cref="MemberIdentifier"/> from a <see cref="Member"/>.
    /// </summary>
    /// <param name="entity">The <see cref="Room"/>.</param>
    /// <returns>A <see cref="RoomIdentifier"/>.</returns>
    public static MemberIdentifier FromEntity(Member entity) => new(entity.User.PlatformUserId, entity.DisplayName);
}

/// <summary>
/// The name and value of room metadata for a room.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Value">The value.</param>
public record RoomMetadataValue(string Name, string? Value);

public record HubDetails(string Id, string Name, DateTime CreatedUtc)
{
    public static HubDetails? FromEntity(Hub? entity) => entity is null
        ? null
        : new(entity.Room.PlatformRoomId, entity.Name, entity.Created);
}
