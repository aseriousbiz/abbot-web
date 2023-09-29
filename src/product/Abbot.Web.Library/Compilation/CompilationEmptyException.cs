using System;

namespace Serious.Abbot.Compilation;

/// <summary>
/// Exception thrown when a compilation emits an empty stream. This is probably due to the
/// code being all comments.
/// </summary>
public class CompilationEmptyException : Exception
{
    /// <summary>
    /// Constructs a <see cref="CompilationEmptyException"/>.
    /// </summary>
    public CompilationEmptyException()
    {
    }

    /// <summary>
    /// Constructs a <see cref="CompilationEmptyException"/> with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public CompilationEmptyException(string message) : base(message)
    {
    }

    /// <summary>
    /// Constructs a <see cref="CompilationEmptyException"/> with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CompilationEmptyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
