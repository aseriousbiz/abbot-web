using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Used to manage and query permissions.
/// </summary>
public interface IPermissionRepository
{
    /// <summary>
    /// Retrieves the capability the member has for the skill.
    /// </summary>
    /// <param name="member">The <see cref="Member"/> to check for permissions.</param>
    /// <param name="skill">The <see cref="Skill"/> to check for permissions.</param>
    /// <returns>The <see cref="Capability"/> the member has for the skill.</returns>
    Task<Capability> GetCapabilityAsync(Member member, Skill skill);

    /// <summary>
    /// Sets a permission for a member and returns the previous capability the user had for the skill.
    /// </summary>
    /// <param name="member">The <see cref="Member"/> to check for permissions.</param>
    /// <param name="skill">The <see cref="Skill"/> to check for permissions.</param>
    /// <param name="capability">The capability to set for the user.</param>
    /// <param name="actor">The <see cref="Member"/> that is changing the permission.</param>
    /// <returns>Returns the previous <see cref="Capability"/> the <see cref="Member"/> had for the <see cref="Skill"/>, if any.</returns>
    Task<Capability> SetPermissionAsync(Member member, Skill skill, Capability capability, Member actor);

    /// <summary>
    /// Retrieves the set of permissions that apply to the specified <see cref="Skill"/>.
    /// </summary>
    /// <param name="skill">The skill to retrieve permissions for.</param>
    Task<IReadOnlyList<Permission>> GetPermissionsForSkillAsync(Skill skill);
}
