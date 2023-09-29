namespace Serious.Abbot.Scripting;

/// <summary>
/// Information about the source skill that raised a signal.
/// </summary>
public interface ISourceSkill : ISignalSource
{
    /// <summary>
    /// The <see cref="ISignalEvent" /> signal that this source skill is responding to, if any.
    /// </summary>
    ISignalEvent? SignalEvent { get; }
}
