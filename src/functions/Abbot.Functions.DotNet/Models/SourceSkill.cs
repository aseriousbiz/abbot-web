using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Models;

/// <summary>
/// Information about the source skill that raised a signal.
/// </summary>
public class SourceSkill : SignalSource, ISourceSkill
{
    readonly SignalMessage? _signalMessage;
    ISignalEvent? _signalEvent;

    /// <summary>
    /// Constructs a <see cref="SourceSkill" /> from a <see cref="SignalSourceMessage"/>.
    /// </summary>
    /// <param name="signalSourceMessage">The <see cref="SignalSourceMessage"/> received from the skill request.</param>
    public SourceSkill(SignalSourceMessage signalSourceMessage) : base(signalSourceMessage)
    {
        _signalMessage = signalSourceMessage.SignalEvent;
    }

    /// <summary>
    /// The <see cref="ISignalEvent" /> signal that this source skill is responding to, if any.
    /// </summary>
    public ISignalEvent? SignalEvent
    {
        get {
            _signalEvent ??= _signalMessage is not null
                ? new SignalEvent(_signalMessage)
                : null;
            return _signalEvent;
        }
    }
}
