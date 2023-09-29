using System;
using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Information about the source of a signal.
/// </summary>
public interface ISignalSource
{
    /// <summary>
    /// Name of the skill.
    /// </summary>
    string SkillName { get; }

    /// <summary>
    /// The URL to the skill editor for the skill.
    /// May be <c>null</c> if the signal name starts with "system:" (for signals that Abbot raises directly).
    /// </summary>
    Uri? SkillUrl { get; }

    /// <summary>
    /// The arguments supplied to the skill. Does not include the skill name.
    /// </summary>
    IArguments Arguments { get; }

    /// <summary>
    /// The mentioned users (if any).
    /// </summary>
    IReadOnlyList<IChatUser> Mentions { get; }
}
