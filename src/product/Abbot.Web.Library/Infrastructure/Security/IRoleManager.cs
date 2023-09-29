using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Used to manage Abbot roles as well as membership in those roles.
/// </summary>
public interface IRoleManager
{
    /// <summary>
    /// Adds the user to the specified role in the database and in the API.
    /// </summary>
    /// <param name="subject">The user to add the role to.</param>
    /// <param name="roleName">Then name of the role to add to the user.</param>
    /// <param name="actor">The user that initiated the role change.</param>
    /// <param name="staffReason">If set, indicates that the action was taken by staff and provides the reason the action was taken.</param>
    Task AddUserToRoleAsync(Member subject, string roleName, Member actor, string? staffReason = null);

    /// <summary>
    /// Removes the user from the specified role in the database and in the API.
    /// </summary>
    /// <param name="subject">The user to remove the role from.</param>
    /// <param name="roleName">Then name of the role to remove from the user.</param>
    /// <param name="actor">The user that initiated the role change.</param>
    /// <param name="staffReason">If set, indicates that the action was taken by staff and provides the reason the action was taken.</param>
    Task RemoveUserFromRoleAsync(Member subject, string roleName, Member actor, string? staffReason = null);

    /// <summary>
    /// Syncs the set of roles the member has to the principal.
    /// </summary>
    /// <param name="member">The member of the organization</param>
    /// <param name="principal">The current logged in user</param>
    void SyncRolesToPrincipal(Member member, ClaimsPrincipal principal);

    /// <summary>
    /// Updates the roles for the member to match the supplied list.
    /// </summary>
    /// <param name="member">The member of the organization</param>
    /// <param name="roles">The set of roles the member should be in and only in.</param>
    /// <param name="actor">The user that initiated the role change.</param>
    Task SyncRolesFromListAsync(Member member, IReadOnlyCollection<string> roles, Member actor);

    /// <summary>
    /// Creates a role in the API and the database.
    /// </summary>
    /// <param name="name">Name of the role</param>
    /// <param name="description">Description of the role</param>
    Task<Role> CreateRoleAsync(string name, string description);

    /// <summary>
    /// Returns all the members in the specified role for the organization.
    /// </summary>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="organization">The organization.</param>
    Task<IReadOnlyList<Member>> GetMembersInRoleAsync(string roleName, Organization organization);

    /// <summary>
    /// Returns a count of the members in the specified role for the organization.
    /// </summary>
    /// <param name="roleName"></param>
    /// <param name="organization"></param>
    Task<int> GetCountInRoleAsync(string roleName, Organization organization);

    /// <summary>
    /// Returns all the roles in the system.
    /// </summary>
    Task<IReadOnlyList<Role>> GetRolesAsync();

    /// <summary>
    /// Returns a role by name. Throws <see cref="InvalidOperationException"/> if the role doesn't exist.
    /// </summary>
    /// <param name="name">The name of the role.</param>
    Task<Role> GetRoleAsync(string name);

    /// <summary>
    /// Restores an archived (aka Active is <c>false</c>) to be active and a member of the Agent role.
    /// </summary>
    /// <param name="subject">The member to restore.</param>
    /// <param name="actor">The member doing the restoring.</param>
    Task RestoreMemberAsync(Member subject, Member actor);
}
