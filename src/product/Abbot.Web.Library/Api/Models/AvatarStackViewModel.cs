using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

/// <summary>
/// The model for an avatar stack partial.
/// </summary>
/// <param name="Items">The set of avatar stack items.</param>
public record AvatarStackViewModel(IEnumerable<AvatarStackItem> Items)
{
    /// <summary>
    /// Creates an <see cref="AvatarStackViewModel"/> from a set of members and a current organization. If the
    /// member is not in the current organization, the secondary avatar is set to the member's organization.
    /// </summary>
    /// <param name="members">The members to render an avatar for.</param>
    /// <param name="viewerOrganization">The organization of the user viewing this avatar.</param>
    public static AvatarStackViewModel FromConversationMembers(
        IEnumerable<ConversationMember> members, Organization viewerOrganization)
        => FromMembers(members.Select(m => m.Member), viewerOrganization);

    /// <summary>
    /// Creates an <see cref="AvatarStackViewModel"/> from a set of members and a current organization. If the
    /// member is not in the current organization, the secondary avatar is set to the member's organization.
    /// </summary>
    /// <param name="members">The members to render an avatar for.</param>
    /// <param name="viewerOrganization">The organization of the user viewing this avatar.</param>
    /// <param name="titleRetriever">Func used to create a tooltip title for this avatar.</param>
    /// <param name="altTextRetriever">Func used to retrieve the alt text for the avatar.</param>
    public static AvatarStackViewModel FromMembers(
        IEnumerable<Member> members,
        Organization viewerOrganization,
        Func<User, string>? titleRetriever = null,
        Func<User, string>? altTextRetriever = null)
        => new(members.Select(m => AvatarStackItem.FromMember(m, viewerOrganization, titleRetriever, altTextRetriever)));
}

/// <summary>
/// Model for an item in an avatar stack.
/// </summary>
/// <param name="Primary">The primary avatar. Usually the user.</param>
/// <param name="Secondary">The secondary avatar, such as the foreign org in the bottom right.</param>
public record AvatarStackItem(Avatar Primary, Avatar? Secondary = null)
{
    /// <summary>
    /// Retrieves an avatar stack item from a member and a current organization. If the
    /// member is not in the current organization, the secondary avatar is set to the
    /// member's organization.
    /// </summary>
    /// <param name="member">The member to render an avatar for.</param>
    /// <param name="viewerOrganization">The organization of the user viewing this avatar.</param>
    /// <param name="titleRetriever">Func used to create a tooltip title for this avatar.</param>
    /// <param name="altTextRetriever">Func used to retrieve the alt text for the avatar.</param>
    public static AvatarStackItem FromMember(
        Member member,
        Organization viewerOrganization,
        Func<User, string>? titleRetriever = null,
        Func<User, string>? altTextRetriever = null) => new(
        Avatar.FromMember(member, titleRetriever, altTextRetriever),
        member switch
        {
            { IsGuest: true } => Avatar.Guest,
            { OrganizationId: var orgId } when orgId != viewerOrganization.Id => Avatar.FromOrganization(viewerOrganization),
            _ => null,
        });
};

/// <summary>
/// A first responder.
/// </summary>
/// <param name="Url">The url to the avatar.</param>
/// <param name="Title">The title to render.</param>
/// <param name="AltText">The Alt Text to render.</param>
public record Avatar(string? Url, string Title, string AltText)
{
    public static readonly Avatar Guest = new("/img/user-shield-duotone.svg", "Guest", "Guest");

    /// <summary>
    /// Retrieves an Avatar from a User.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="titleRetriever">Func used to create a tooltip title for this avatar.</param>
    /// <param name="altTextRetriever">Func used to retrieve the alt text for the avatar.</param>
    public static Avatar FromUser(
        User user,
        Func<User, string>? titleRetriever = null,
        Func<User, string>? altTextRetriever = null) => new(
        user.Avatar,
        titleRetriever?.Invoke(user) ?? user.DisplayName,
        altTextRetriever?.Invoke(user) ?? user.DisplayName);

    /// <summary>
    /// Creates an avatar from a member.
    /// </summary>
    /// <param name="member">The member to retrieve the avatar for.</param>
    /// <param name="titleRetriever">Func used to create a tooltip title for this avatar.</param>
    /// <param name="altTextRetriever">Func used to retrieve the alt text for the avatar.</param>
    public static Avatar FromMember(
        Member member,
        Func<User, string>? titleRetriever = null,
        Func<User, string>? altTextRetriever = null) => FromUser(member.User, titleRetriever, altTextRetriever);

    public static Avatar FromOrganization(Organization organization) => new(
        organization.Avatar,
        organization.Name ?? "unknown",
        organization.Name ?? "unknown");
};
