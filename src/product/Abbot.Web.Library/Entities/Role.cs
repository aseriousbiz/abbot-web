using System.Collections.Generic;

namespace Serious.Abbot.Entities;

/// <summary>
/// A role within the application such as Administrator, Member, etc.
/// </summary>
public class Role : EntityBase<Role>
{
    /// <summary>
    /// The name of the role.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// A description of the role.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// All the user role memberships.
    /// </summary>
    public IList<MemberRole> MemberRoles { get; set; } = new List<MemberRole>();
}
