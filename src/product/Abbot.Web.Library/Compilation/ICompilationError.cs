namespace Serious.Abbot.Compilation;

/// <summary>
/// Represents a compilation error.
/// </summary>
public interface ICompilationError
{
    /// <summary>
    /// The compilation error id.
    /// </summary>
    string ErrorId { get; }

    /// <summary>
    /// A description of the compilation error.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The line where the error starts. This is 0-based.
    /// </summary>
    int LineStart { get; }

    /// <summary>
    /// The line where the error ends. This is 0-based.
    /// </summary>
    int LineEnd { get; }

    /// <summary>
    /// The position in the line where the error starts.
    /// </summary>
    int SpanStart { get; }

    /// <summary>
    /// The position in the line where the error ends.
    /// </summary>
    int SpanEnd { get; }
}
