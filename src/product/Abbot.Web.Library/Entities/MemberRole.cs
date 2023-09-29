namespace Serious.Abbot.Entities;

/// <summary>
/// The mapping of a user to a role.
/// </summary>
public class MemberRole
{
    /// <summary>
    /// The Id of the <see cref="Entities.Member"/> mapped to a role.
    /// </summary>
    public int MemberId { get; set; }

    /// <summary>
    /// The <see cref="Entities.Member"/> mapped to a role.
    /// </summary>
    public Member Member { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Role"/> mapped to a user.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// The <see cref="Role"/> mapped to a user.
    /// </summary>
    public Role Role { get; set; } = null!;
}
