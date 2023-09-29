namespace Serious.Abbot.Messages;

public enum CompilationRequestType
{
    /// <summary>
    /// A request for the cached compilation.
    /// </summary>
    Cached,

    /// <summary>
    /// A request to recompile the skill.
    /// </summary>
    Recompile,

    /// <summary>
    /// A request for the cached compilation symbols.
    /// </summary>
    Symbols
}
