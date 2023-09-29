using System;
using System.Collections.Generic;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// The source of a signal. This is used to populate a
/// <see cref="ISignalSource"/> or an <see cref="IRootSourceSkill"/>.
/// </summary>
public class SignalSourceMessage
{
    /// <summary>
    /// The Audit Id for the skill run associated with this source.
    /// </summary>
    public Guid AuditIdentifier { get; init; }

    // PROPERTIES COMMON TO ALL SIGNAL SOURCES

    /// <summary>
    /// Name of the skill.
    /// </summary>
    public string SkillName { get; init; } = null!;

    /// <summary>
    /// The URL to the skill editor for the skill.
    /// </summary>
    public Uri? SkillUrl { get; init; }

    /// <summary>
    /// The arguments supplied to the skill. Does not include the skill name.
    /// </summary>
    public string Arguments { get; init; } = null!;

    /// <summary>
    /// The mentioned users (if any).
    /// </summary>
    public IReadOnlyList<PlatformUser> Mentions { get; init; } = null!;

    /// <summary>
    /// The <see cref="SignalMessage" /> signal that this source skill is responding to, if any.
    /// </summary>
    public SignalMessage? SignalEvent { get; init; }

    // PROPERTIES ONLY AVAILABLE FOR ROOT SIGNALS

    /// <summary>
    /// If true, the skill is responding to an HTTP trigger request. The request information can be accessed via
    /// the <see cref="Request"/> property.
    /// </summary>
    public bool? IsRequest { get; init; }

    /// <summary>
    /// If true, the skill is responding to the user interacting with a UI element in chat such as clicking on
    /// a button.
    /// </summary>
    public bool? IsInteraction { get; init; }

    /// <summary>
    /// If true, the skill is responding to a chat message.
    /// </summary>
    public bool? IsChat { get; init; }

    /// <summary>
    /// If true, the skill is responding to a chat message because it matched a pattern, not because it was
    /// directly called.
    /// </summary>
    public bool? IsPatternMatch { get; init; }

    /// <summary>
    /// If the skill is responding to a pattern match, then this contains information about the pattern that
    /// matched the incoming message and caused this skill to be called. Otherwise this is null.
    /// </summary>
    public PatternMessage? Pattern { get; init; }

    /// <summary>
    /// If called by an HTTP trigger, this contains information about the request.
    /// </summary>
    public HttpTriggerRequest? Request { get; init; }
}
