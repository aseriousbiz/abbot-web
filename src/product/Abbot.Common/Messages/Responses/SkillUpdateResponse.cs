namespace Serious.Abbot.Messages;

public class SkillUpdateResponse
{
    /// <summary>
    /// If true, then there were changes and they were saved. If false, no changes were saved.
    /// </summary>
    public bool Updated { get; set; }

    /// <summary>
    /// The updated SHA1 hash of the skill code as denoted by the server.
    /// </summary>
    public string NewCodeHash { get; set; } = string.Empty;
}
