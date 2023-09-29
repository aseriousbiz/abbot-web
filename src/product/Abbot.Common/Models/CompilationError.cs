using System.Diagnostics;

namespace Serious.Abbot.Messages.Models;

/// <summary>
/// Represents a compilation error when compiling a user skill.
/// </summary>
[DebuggerDisplay("{Description} Lines: {LineStart}:{LineEnd}")]
public class CompilationError
{
    /// <summary>
    /// The Id of the error.
    /// </summary>
    public string ErrorId { get; set; } = null!;

    /// <summary>
    /// A description of the error.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// The line start for the error.
    /// </summary>
    public int LineStart { get; set; }

    /// <summary>
    /// The last line of the error.
    /// </summary>
    public int LineEnd { get; set; }

    /// <summary>
    /// Where the error starts in the line.
    /// </summary>
    public int SpanStart { get; set; }

    /// <summary>
    /// Where the error ends in the line.
    /// </summary>
    public int SpanEnd { get; set; }
}
