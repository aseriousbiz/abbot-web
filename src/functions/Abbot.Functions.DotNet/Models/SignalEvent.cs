using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Models;

/// <summary>
/// A signal raised by a skill.
/// </summary>
public class SignalEvent : ISignalEvent
{
    readonly SignalMessage _signalMessage;
    ISourceSkill? _source;
    IRootSourceSkill? _root;

    /// <summary>
    /// Constructs a <see cref="SignalEvent"/> using the incoming <see cref="SignalMessage"/>.
    /// </summary>
    /// <param name="signalMessage"></param>
    public SignalEvent(SignalMessage signalMessage)
    {
        _signalMessage = signalMessage;
    }

    /// <summary>
    /// The name of the signal.
    /// </summary>
    public string Name => _signalMessage.Name;

    /// <summary>
    /// The signal arguments the source raised.
    /// </summary>
    public string Arguments => _signalMessage.Arguments;

    /// <summary>
    /// The skill that raised this signal.
    /// </summary>
    /// <returns>The <see cref="ISourceSkill" /> that raised this signal.</returns>
    public ISourceSkill Source => _source ??= new SourceSkill(_signalMessage.Source);

    /// <summary>
    /// The skill that started this chain of signals.
    /// </summary>
    /// <returns>The <see cref="IRootSourceSkill"/> that started this chain of signals.</returns>
    public IRootSourceSkill RootSource => _root ??= GetRootSource();

    IRootSourceSkill GetRootSource()
    {
        var source = _signalMessage.Source;
        while (source.SignalEvent is not null)
        {
            source = source.SignalEvent.Source;
        }

        return new RootSourceSkill(source);
    }
}
