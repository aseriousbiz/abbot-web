using Serious.Abbot.Messages.Models;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents a runtime error when running a user skill in a skill runner.
/// </summary>
public class RuntimeError : CompilationError
{
    /// <summary>
    /// The stack trace of the runtime error.
    /// </summary>
    public string? StackTrace { get; set; }
}
