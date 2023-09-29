namespace Serious.Abbot.Messages;

/// <summary>
/// How to order the returned skills.
/// </summary>
public enum SkillOrderBy
{
    /// <summary>
    /// Order by name ascending.
    /// </summary>
    Name,

    /// <summary>
    /// Order by the create date.
    /// </summary>
    Created,

    /// <summary>
    /// Order by date last modified.
    /// </summary>
    Modified,
}
