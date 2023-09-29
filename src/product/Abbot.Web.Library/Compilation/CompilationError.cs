using System.Diagnostics;
using System.Globalization;
using Ink;
using Microsoft.CodeAnalysis;

namespace Serious.Abbot.Compilation;

[DebuggerDisplay("{Description} Lines: {LineStart}:{LineEnd}")]
public sealed class CompilationError : ICompilationError
{
    /// <summary>
    /// Creates a compilation error based on the <see cref="Diagnostic"/> received from the Roslyn compiler.
    /// </summary>
    /// <param name="diagnostic">A Roslyn diagnostic.</param>
    public static ICompilationError Create(Diagnostic diagnostic)
    {
        return new CompilationError(diagnostic);
    }

    /// <summary>
    /// Creates a compilation error with the specified description. This is used in the case
    /// where the compiler crashes and doesn't give us any useful information.
    /// </summary>
    /// <param name="description"></param>
    public static ICompilationError Create(string description)
    {
        return new CompilationError("Unknown", description);
    }

    public static ICompilationError Create(ErrorType errorType, string description, int line, int pos) =>
        new CompilationError(errorType, description, line, pos);

    public CompilationError(Diagnostic diagnostic)
        : this(diagnostic.Id, diagnostic.GetMessage(CultureInfo.InvariantCulture))
    {
        var linePositionSpan = diagnostic.Location.GetLineSpan();
        LineStart = linePositionSpan.StartLinePosition.Line;
        LineEnd = linePositionSpan.EndLinePosition.Line;
        SpanStart = linePositionSpan.Span.Start.Character;
        SpanEnd = linePositionSpan.Span.End.Character;
    }

    public CompilationError(ErrorType errorType, string description, int line, int pos)
    {
        ErrorId = errorType.ToString();
        Description = description;
        LineStart = LineEnd = line;
        SpanStart = SpanEnd = pos;
    }

    CompilationError(string errorId, string description)
    {
        ErrorId = errorId;
        Description = description;
    }

    public string ErrorId { get; }
    public string Description { get; }
    public int LineStart { get; }
    public int LineEnd { get; }
    public int SpanStart { get; }
    public int SpanEnd { get; }

    public override string ToString() => LineStart >= 0
        ? $"[{ErrorId}] <skill-code>:{LineStart} {Description}"
        : $"[{ErrorId}] {Description}";
}
