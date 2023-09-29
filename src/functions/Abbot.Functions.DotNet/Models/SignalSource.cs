using System;
using System.Collections.Generic;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Models;

/// <summary>
/// Information about the source of a signal.
/// </summary>
public class SignalSource : ISignalSource
{
    readonly SignalSourceMessage _sourceSkillMessage;
    IArguments? _arguments;

    /// <summary>
    /// Constructs a <see cref="SignalSource"/>.
    /// </summary>
    /// <param name="sourceSkillMessage">The <see cref="SignalSourceMessage"/> received from the skill request.</param>
    protected SignalSource(SignalSourceMessage sourceSkillMessage)
    {
        _sourceSkillMessage = sourceSkillMessage;
    }

    /// <summary>
    /// Name of the source skill.
    /// </summary>
    public string SkillName => _sourceSkillMessage.SkillName;

    /// <summary>
    /// The URL to the skill editor for the skill.
    /// </summary>
    public Uri? SkillUrl => _sourceSkillMessage.SkillUrl;

    /// <summary>
    /// The arguments supplied to the source skill. Does not include the skill name.
    /// </summary>
    public IArguments Arguments => _arguments
        ??= new Arguments(_sourceSkillMessage.Arguments, _sourceSkillMessage.Mentions);

    /// <summary>
    /// The mentioned users (if any).
    /// </summary>
    public IReadOnlyList<IChatUser> Mentions => _sourceSkillMessage.Mentions;
}
