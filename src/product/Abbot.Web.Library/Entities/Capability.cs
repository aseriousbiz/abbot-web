namespace Serious.Abbot.Entities;

/// <summary>
/// A capability that a user has.
/// </summary>
public enum Capability
{
    /// <summary>
    /// No capability. Returned when a skill or member does not exist.
    /// </summary>
    None = 0,

    /// <summary>
    /// Allowed to view or read a skill. Not used right now.
    /// </summary>
    /// Read = 1,

    /// <summary>
    /// Allowed to run a skill.
    /// </summary>
    Use = 2,

    /// <summary>
    /// Allowed to edit a skill.
    /// </summary>
    Edit = 3,

    /// <summary>
    /// Allowed to administer a skill such as changing permissions for the skill.
    /// </summary>
    Admin = 4
}
