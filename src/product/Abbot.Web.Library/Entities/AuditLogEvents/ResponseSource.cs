namespace Serious.Abbot.Entities;

public enum ResponseSource
{
    /// <summary>
    /// Responded with the auto-responder.
    /// </summary>
    AutoResponder,

    /// <summary>
    /// Responded with a set of similar skills from search.
    /// </summary>
    SkillSearch,
}
