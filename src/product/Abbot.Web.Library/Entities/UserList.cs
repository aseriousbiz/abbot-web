using System.Collections.Generic;

namespace Serious.Abbot.Entities;

/// <summary>
/// A list of entries created and maintained by users. The list stores
/// the information for the `list` skill.
/// </summary>
public class UserList : SkillEntityBase<UserList>
{
    /// <summary>
    /// The set of entries in the list.
    /// </summary>
    public List<UserListEntry> Entries { get; set; } = new List<UserListEntry>();
}
