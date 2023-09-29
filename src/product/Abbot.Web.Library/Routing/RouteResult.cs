using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Skills;

namespace Serious.Abbot.Infrastructure;

/// <summary>
/// The result of an attempt to route an incoming message to a skill.
/// </summary>
/// <param name="Context">Information about the current incoming message or event.</param>
/// <param name="Skill">The object that will handle the incoming message or event, if any.</param>
/// <param name="IsDirectedAtBot">Indicates if the command was originally directed at the bot.</param>
/// <param name="IsPatternMatch">Indicates that the command was matched by a pattern and should be directed to a skill.</param>
public record RouteResult(MessageContext Context, ISkill? Skill, bool IsDirectedAtBot, bool IsPatternMatch)
{
    /// <summary>
    /// The result does not match a message that Abbot should ever process.
    /// NOTE: This indicates a message that Abbot should _completely ignore_ (including for features like Conversation Tracking).
    /// </summary>
    public static readonly RouteResult Ignore = new(default!, default, false, false);
}

public record PayloadHandlerRouteResult(IPayloadHandlerInvoker HandlerInvoker)
{
    /// <summary>
    /// The result does not match a message that Abbot should ever process.
    /// NOTE: This indicates a message that Abbot should _completely ignore_ (including for features like Conversation Tracking).
    /// </summary>
    public static readonly PayloadHandlerRouteResult Ignore = new(default(IPayloadHandlerInvoker)!);
};
