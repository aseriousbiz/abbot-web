namespace Serious.Abbot.Scripting;

/// <summary>
/// <para>
/// The pattern that caused a skill to be invoked. Most skills are called by name. For example, by mentioning
/// Abbot followed by the skill name, or by using the shortcut character followed  by the skill name. For
/// example, <code>.help</code> calls the <code>help</code> skill.
/// </para>
/// <para>
/// A pattern is another way a skill can be called. When a message that is NOT an Abbot command matches a
/// skill's pattern, the skill is called with the message as the arguments. This interface describes such a
/// pattern.
/// </para>
/// </summary>
public interface IPattern
{
    /// <summary>
    /// A friendly name for the pattern.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// An optional description of the pattern.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// The type of pattern.
    /// </summary>
    PatternType PatternType { get; }

    /// <summary>
    /// The pattern to match.
    /// </summary>
    string Pattern { get; }

    /// <summary>
    /// Whether or not the pattern is case sensitive. By default, it is not.
    /// </summary>
    bool CaseSensitive { get; }
}
