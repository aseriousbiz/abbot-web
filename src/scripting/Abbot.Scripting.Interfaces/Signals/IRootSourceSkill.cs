namespace Serious.Abbot.Scripting;

/// <summary>
/// The skill that is the root source of a signal. This is the skill that kicked off the current signal
/// chain.
/// </summary>
public interface IRootSourceSkill : ISignalSource
{
    /// <summary>
    /// If <see cref="IsRequest"/> is true, then the skill is responding to an HTTP trigger request
    /// (instead of a chat message) and this property is populated with the incoming request information.
    /// </summary>
    IHttpTriggerEvent? Request { get; }

    /// <summary>
    /// If true, the skill is responding to an HTTP trigger request. The request information can be accessed via
    /// the <see cref="Request"/> property.
    /// </summary>
    bool IsRequest { get; }

    /// <summary>
    /// If true, the skill is responding to the user interacting with a UI element in chat such as clicking on
    /// a button.
    /// </summary>
    bool IsInteraction { get; }

    /// <summary>
    /// If true, the skill is responding to a chat message.
    /// </summary>
    bool IsChat { get; }

    /// <summary>
    /// If true, the skill is responding to a chat message because it matched a pattern, not because it was
    /// directly called.
    /// </summary>
    bool IsPatternMatch { get; }

    /// <summary>
    /// If the skill is responding to a pattern match, then this contains information about the pattern that
    /// matched the incoming message and caused this skill to be called. Otherwise this is null.
    /// </summary>
    IPattern? Pattern { get; }
}
