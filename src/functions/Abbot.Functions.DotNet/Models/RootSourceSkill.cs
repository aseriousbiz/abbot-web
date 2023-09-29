using System;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Models;

/// <summary>
/// The skill that is the root source of a signal. This is the skill that kicked off the current signal
/// chain.
/// </summary>
public class RootSourceSkill : SignalSource, IRootSourceSkill
{
    readonly SignalSourceMessage _rootSourceSkill;
    IHttpTriggerEvent? _httpTriggerEvent;

    /// <summary>
    /// Constructs a <see cref="RootSourceSkill" /> from a <see cref="SignalSourceMessage"/>.
    /// </summary>
    /// <param name="rootSourceSkill">The <see cref="SignalSourceMessage"/> received from the skill request.</param>
    public RootSourceSkill(SignalSourceMessage rootSourceSkill) : base(rootSourceSkill)
    {
        if (rootSourceSkill.SignalEvent is not null)
        {
            throw new ArgumentException($"The {nameof(SignalSourceMessage)} with name `{rootSourceSkill.SkillName}` must be a root source.", nameof(rootSourceSkill));
        }

        _rootSourceSkill = rootSourceSkill;
    }

    /// <summary>
    /// If <see cref="IsRequest"/> is true, then the skill is responding to an HTTP trigger request
    /// (instead of a chat message) and this property is populated with the incoming request information.
    /// </summary>
    public IHttpTriggerEvent? Request => _httpTriggerEvent ??= _rootSourceSkill.Request is not null
        ? new HttpTriggerEvent(_rootSourceSkill.Request)
        : null;

    /// <summary>
    /// If true, the skill is responding to an HTTP trigger request. The request information can be accessed via
    /// the <see cref="Request"/> property.
    /// </summary>
    public bool IsRequest => _rootSourceSkill.IsRequest.GetValueOrDefault();

    /// <summary>
    /// If true, the skill is responding to the user interacting with a UI element in chat such as clicking on
    /// a button.
    /// </summary>
    public bool IsInteraction => _rootSourceSkill.IsInteraction.GetValueOrDefault();

    /// <summary>
    /// If true, the skill is responding to a chat message.
    /// </summary>
    public bool IsChat => _rootSourceSkill.IsChat.GetValueOrDefault();

    /// <summary>
    /// If true, the skill is responding to a chat message because it matched a pattern, not because it was
    /// directly called.
    /// </summary>
    public bool IsPatternMatch => _rootSourceSkill.IsPatternMatch.GetValueOrDefault();

    /// <summary>
    /// If the skill is responding to a pattern match, then this contains information about the pattern that
    /// matched the incoming message and caused this skill to be called. Otherwise this is null.
    /// </summary>
    public IPattern? Pattern => _rootSourceSkill.Pattern;
}
