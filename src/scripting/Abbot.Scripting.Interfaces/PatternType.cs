namespace Serious.Abbot.Scripting;

/// <summary>
/// How a pattern or signal should be used to match incoming messages.
/// </summary>
/// <remarks>
/// ALWAYS set an explicit integer value for each entry, and DO NOT change the existing ones, no matter how tempting
/// it may be to reorder them for "consistency".
/// </remarks>
public enum PatternType
{
    /// <summary>
    /// Pattern doesn't match anything. Used to disable a pattern.
    /// </summary>
    None = 0,

    /// <summary>
    /// Matches messages that start with the pattern.
    /// </summary>
    StartsWith = 1,

    /// <summary>
    /// Matches messages that end with the pattern.
    /// </summary>
    EndsWith = 2,

    /// <summary>
    /// Matches messages where the message contains the pattern.
    /// </summary>
    Contains = 3,

    /// <summary>
    /// This pattern uses regular expressions to match incoming messages.
    /// </summary>
    RegularExpression = 4,

    /// <summary>
    /// This pattern matches messages that are an exact match.
    /// </summary>
    ExactMatch = 5,
}
