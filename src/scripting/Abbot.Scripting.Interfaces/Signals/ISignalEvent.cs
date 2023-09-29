namespace Serious.Abbot.Scripting;

/// <summary>
/// A signal raised by a skill.
/// </summary>
public interface ISignalEvent
{
    /// <summary>
    /// The name of the signal.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The signal arguments the source raised.
    /// </summary>
    string Arguments { get; }

    /// <summary>
    /// The skill that raised this signal.
    /// </summary>
    /// <returns>The <see cref="ISourceSkill" /> that raised this signal.</returns>
    ISourceSkill Source { get; }

    /// <summary>
    /// The skill that started this chain of signals.
    /// </summary>
    /// <returns>The <see cref="IRootSourceSkill"/> that started this chain of signals.</returns>
    IRootSourceSkill RootSource { get; }
}
