namespace Serious.Abbot.Messages;

/// <summary>
/// Response from a request to the Skill API for a data
/// item that belongs to a skill.
/// </summary>
public class SkillDataResponse
{
    /// <summary>
    /// The key of the data item.
    /// </summary>
    public string Key { get; init; } = null!;

    /// <summary>
    /// The value of the data item.
    /// </summary>
    public string Value { get; init; } = null!;
}
