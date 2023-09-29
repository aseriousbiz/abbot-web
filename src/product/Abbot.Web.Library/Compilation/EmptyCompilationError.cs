namespace Serious.Abbot.Compilation;

/// <summary>
/// Compilation error when the compilation is empty such as when every line is a comment.
/// </summary>
public class EmptyCompilationError : ICompilationError
{
    public string ErrorId => "EmptyCompilation";

    public string Description =>
        "Code consisting only of comments cannot be saved as a skill.";

    public int LineStart => 0;
    public int LineEnd => 0;
    public int SpanStart => 0;
    public int SpanEnd => 0;
}
