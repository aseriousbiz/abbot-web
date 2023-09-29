using Serious.Abbot.Messages;
using Serious.Abbot.Repositories;
using Serious.Slack;
using Serious.Slack.Events;

namespace Serious.Abbot.Events;

/// <summary>
/// The payload for the event when the bot is removed from Slack.
/// </summary>
/// <remarks>
/// The event is handled in <see cref="MetaBot" />. The actual uninstall code is in
/// <see cref="OrganizationRepository"/>.
/// </remarks>
/// <param name="PlatformId">The team or org id on the chat platform. For Slack this typically starts with "T" such as "T0123456789".</param>
/// <param name="BotAppId">The Slack App ID.</param>
public record UninstallPayload(string PlatformId, string BotAppId) : IOrganizationIdentifier;

/// <summary>
/// Represents an event when a room is updated on the chat platform.
/// </summary>
/// <param name="PlatformRoomId">The Platform-specific id for the room.</param>
public record struct RoomEventPayload(string PlatformRoomId) : IRoomPayload;

/// <summary>
/// Represents the type of membership change represented by a <see cref="RoomMembershipEventPayload"/>.
/// </summary>
public enum MembershipChangeType
{
    /// <summary>
    /// A member was added to the room.
    /// </summary>
    Added,

    /// <summary>
    /// A member was removed from the room.
    /// </summary>
    Removed,
}

/// <summary>
/// Represents an event when a room's membership is updated on the chat platform.
/// </summary>
/// <param name="Type">The type of membership change.</param>
/// <param name="PlatformRoomId">The Platform-specific id for the room.</param>
/// <param name="PlatformUserId">The Platform-specific id for the user.</param>
/// <param name="InviterPlatformUserId">The Platform-specific id for the user that invited this user, if any.</param>
public record struct RoomMembershipEventPayload(
    MembershipChangeType Type,
    string PlatformRoomId,
    string PlatformUserId,
    string? InviterPlatformUserId = null) : IRoomPayload;

/// <summary>
/// Represents an event where user info changed on the chat platform. For example, when the TimeZone
/// for a user changes on Slack.
/// </summary>
/// <param name="Id">The platform specific Id of the user.</param>
/// <param name="PlatformId">The platform specific Id of the organization this user belongs to. May be null if the user is in the same team as the originating message.</param>
/// <param name="RealName">The real name of the user.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Email">The Email of the user, if known.</param>
/// <param name="TimeZoneId">The user's new timezone, if changed.</param>
/// <param name="Avatar">The user's updated avatar.</param>
/// <param name="Deleted">If true, this user was deleted from the chat platform and we should inactivate them..</param>
/// <param name="IsBot">If true, this event applies to a bot and we should ignore it.</param>
/// <param name="IsGuest">If <c>true</c>, the user is a guest account.</param>
public record UserEventPayload(
    string Id,
    string? PlatformId,
    string RealName,
    string DisplayName,
    string? Email = null,
    string? TimeZoneId = null,
    string? Avatar = null,
    bool Deleted = false,
    bool IsBot = false,
    bool IsGuest = false)
{
    public static UserEventPayload FromSlackUserInfo(UserInfo slackUser)
    {
        return new UserEventPayload(
            slackUser.Id,
            slackUser.TeamId,
            slackUser.Profile.RealName ?? "",
            // Slack deprecated the UserName field, so we use the DisplayName instead.
            // https://api.slack.com/changelog/2017-09-the-one-about-usernames
            slackUser.Profile switch
            {
                { DisplayName: { Length: > 0 } dn } => dn,
                { RealName: { Length: > 0 } rn } => rn,
                _ => slackUser.Name ?? "",
            },
            slackUser.Profile.Email,
            slackUser.TimeZone,
            slackUser.Profile.Image72,
            slackUser.Deleted,
            slackUser.IsBot,
            slackUser.IsRestricted);
    }
}

/// <summary>
/// An normalized team change event that wraps the Slack <c>team_domain_change</c> and <c>team_rename</c> events
/// so we only need a single payload handler when an organization changes.
/// </summary>
/// <param name="TeamId">The Id of the team that changed.</param>
public readonly record struct TeamChangeEventPayload(string TeamId)
{
    /// <summary>
    /// Create a <see cref="TeamChangeEventPayload"/> from a <see cref="TeamDomainChangeEvent"/>.
    /// </summary>
    /// <param name="domainChangeEvent">The incoming <c>team_domain_change</c>.</param>
    /// <returns>Returns a <see cref="TeamDomainChangeEvent"/>.</returns>
    public static TeamChangeEventPayload FromTeamDomainChangeEvent(TeamDomainChangeEvent domainChangeEvent)
    {
        return new TeamChangeEventPayload(domainChangeEvent.TeamId);
    }

    /// <summary>
    /// Create a <see cref="TeamChangeEventPayload"/> from a <see cref="TeamRenameEvent"/>.
    /// </summary>
    /// <param name="teamRenameEvent">The incoming <c>team_domain_change</c>.</param>
    /// <returns>Returns a <see cref="TeamChangeEventPayload"/>.</returns>
    public static TeamChangeEventPayload FromTeamRenameEvent(TeamRenameEvent teamRenameEvent)
    {
        return new TeamChangeEventPayload(teamRenameEvent.TeamId);
    }
}
