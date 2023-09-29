using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

public static class PermissionExtensions
{
    /// <summary>
    /// Returns <c>true</c> if the member has permission to run the skill.
    /// </summary>
    /// <param name="permissionRepository">The permissions repository.</param>
    /// <param name="member">The <see cref="Member"/> to check for permission.</param>
    /// <param name="skill">The <see cref="Skill"/> to check for permission.</param>
    /// <returns><c>true</c> if the user can run the skill, otherwise <c>false</c>.</returns>
    public static async Task<bool> CanRunAsync(
        this IPermissionRepository permissionRepository,
        Member member,
        Skill skill)
    {
        return !skill.Restricted
               || await permissionRepository.GetCapabilityAsync(member, skill) >= Capability.Use;
    }

    /// <summary>
    /// Returns true if the member has permission to edit the skill.
    /// </summary>
    /// <param name="permissionRepository">The permissions repository.</param>
    /// <param name="member">The <see cref="Member"/> to check for permission.</param>
    /// <param name="skill">The <see cref="Skill"/> to check for permission.</param>
    /// <returns><c>true</c> if the user can edit the skill, otherwise <c>false</c>.</returns>
    public static async Task<bool> CanEditAsync(
        this IPermissionRepository permissionRepository,
        Member member,
        Skill skill)
    {
        if (member.OrganizationId != skill.OrganizationId)
        {
            // We NEVER allow editing of skills from other orgs.
            return false;
        }
        return !skill.Restricted
               || await permissionRepository.GetCapabilityAsync(member, skill) >= Capability.Edit;
    }

    /// <summary>
    /// Returns true if the member has permission to administrate the skill.
    /// </summary>
    /// <param name="permissionRepository">The permissions repository.</param>
    /// <param name="member">The <see cref="Member"/> to check for permission.</param>
    /// <param name="skill">The <see cref="Skill"/> to check for permission.</param>
    /// <returns><c>true</c> if the user can edit the skill, otherwise <c>false</c>.</returns>
    public static async Task<bool> CanAdministrateAsync(
        this IPermissionRepository permissionRepository,
        Member member,
        Skill skill)
    {
        if (member.OrganizationId != skill.OrganizationId)
        {
            // We NEVER allow administration of skills from other orgs.
            return false;
        }

        var permission = await permissionRepository.GetCapabilityAsync(member, skill);
        return permission >= Capability.Admin;
    }
}
