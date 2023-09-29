using System;
using System.Collections.Generic;

namespace Serious.Abbot.Messages;

/// <summary>
/// A response when running a skill fails.
/// </summary>
public class RuntimeErrorResponse : ErrorResponse
{
    /// <summary>
    /// The set of errors returned by the skill runner.
    /// </summary>
    public IReadOnlyList<RuntimeError> Errors { get; set; } = Array.Empty<RuntimeError>();
}
