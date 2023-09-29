using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;

namespace Serious.Abbot.Signals;

/// <summary>
/// Handles incoming signals raised from a user skill.
/// </summary>
public interface ISignalHandler
{
    /// <summary>
    /// Receives a <see cref="SignalRequest"/> and enqueues a call to all skills in the organization
    /// subscribed to that signal.
    /// </summary>
    /// <param name="skillId">The ID of the skill raising the signal.</param>
    /// <param name="signalRequest">The <see cref="SignalRequest"/> containing information about the signal to create.</param>
    /// <returns>True if signal handling was enqueued successfully. Returns false if this signal request would lead to a cycle (i.e. this signal was already raised in the signal chain).</returns>
    bool EnqueueSignalHandling(Id<Skill> skillId, SignalRequest signalRequest);
}

/// <summary>
/// Used to raise system signals.
/// </summary>
public interface ISystemSignaler
{
    /// <summary>
    /// Raises a system signal.
    /// </summary>
    /// <param name="signal">The signal to raise.</param>
    /// <param name="arguments">The arguments to pass to the signal subscribers.</param>
    /// <param name="organizationId">The ID of the organization in which to raise the signal.</param>
    /// <param name="room">The room in which the system signal should be raised.</param>
    /// <param name="actor">The actor that caused the signal to be raised.</param>
    /// <param name="triggeringMessage">A <see cref="MessageInfo"/> indicating the message that triggered this signal, if any.</param>
    void EnqueueSystemSignal(
        SystemSignal signal,
        string arguments,
        Id<Organization> organizationId,
        PlatformRoom room,
        Member actor,
        MessageInfo? triggeringMessage);
}
