namespace Serious.Abbot.Messages;

/// <summary>
/// When calling a skill via a signal, this contains information about the signal that is causing the
/// skill to be invoked.
/// </summary>
public class SignalMessage
{
    /// <summary>
    /// The name of the signal.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// The signal arguments the source raised.
    /// </summary>
    public string Arguments { get; set; } = null!;

    /// <summary>
    /// The skill that raised this signal.
    /// </summary>
    /// <returns>The <see cref="SignalSourceMessage" /> that raised this signal.</returns>
    public SignalSourceMessage Source { get; set; } = null!;
}
