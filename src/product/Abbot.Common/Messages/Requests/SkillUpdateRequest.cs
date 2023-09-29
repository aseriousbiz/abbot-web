namespace Serious.Abbot.Messages;

public class SkillUpdateRequest
{
    /// <summary>
    /// The new code.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// A SHA1 hash of the code used to uniquely identify the code and determine if the code has changed.
    /// When updating a skill, this is the hash of the code prior to the change this update request represents.
    /// </summary>
    public string PreviousCodeHash { get; set; } = string.Empty;
}
