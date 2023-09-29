using System;
using System.Collections.Generic;
using System.Linq;

namespace Serious.Abbot.Messages;

/// <summary>
/// A request to create a signal.
/// </summary>
public class SignalRequest
{
    /// <summary>
    /// The name of the signal.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The signal arguments the source raised.
    /// </summary>
    public string Arguments { get; set; } = null!;

    /// <summary>
    /// The room (or channel) name this skill is responding to.
    /// </summary>
    public required PlatformRoom Room { get; set; }

    /// <summary>
    /// The Id of the member that is raising the signal.
    /// </summary>
    public int SenderId { get; set; }

    /// <summary>
    /// Information about the source of this signal.
    /// </summary>
    public SignalSourceMessage Source { get; set; } = null!;

    /// <summary>
    /// The ID of the conversation this signal was raised in, if any.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Checks to see if this <see cref="SignalRequest"/> would lead to a cycle. In other words, tests to see
    /// if the signal was already raised in the signal chain.
    /// </summary>
    public bool ContainsCycle()
    {
        return _containsCycle ??= SignalChain.Contains(Name, StringComparer.OrdinalIgnoreCase);
    }

    bool? _containsCycle;

    IEnumerable<string> SignalChain
    {
        get {
            var sourceSkill = Source;
            while (sourceSkill is { SignalEvent: { } })
            {
                var signalEvent = sourceSkill.SignalEvent;
                yield return signalEvent.Name;
                sourceSkill = signalEvent.Source;
            }
        }
    }
}
