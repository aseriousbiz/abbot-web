using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents a matched pattern in a serializable format. This is what we pass to Skill Runners.
/// </summary>
public class PatternMessage : IPattern
{
    /// <summary>
    /// Constructs a <see cref="PatternMessage"/>.
    /// </summary>
    public PatternMessage()
    {
    }

    /// <summary>
    /// Constructs a <see cref="PatternMessage" /> by copying relevant properties of the
    /// specified <see cref="IPattern"/>.
    /// </summary>
    /// <param name="pattern">The pattern to copy.</param>
    public PatternMessage(IPattern pattern)
    {
        Name = pattern.Name;
        Pattern = pattern.Pattern;
        Description = pattern.Description;
        PatternType = pattern.PatternType;
        CaseSensitive = pattern.CaseSensitive;
    }

    /// <summary>
    /// A friendly name for the pattern.
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// An optional description of the pattern.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The type of pattern.
    /// </summary>
    public PatternType PatternType { get; init; }

    /// <summary>
    /// The pattern to match.
    /// </summary>
    public string Pattern { get; init; } = null!;

    /// <summary>
    /// Whether or not the pattern is case sensitive. By default, it is not.
    /// </summary>
    public bool CaseSensitive { get; init; }

    public override string ToString()
    {
        return $"{PatternType} `{Pattern}`{(CaseSensitive ? " (Case Sensitive)" : string.Empty)}";
    }
}
