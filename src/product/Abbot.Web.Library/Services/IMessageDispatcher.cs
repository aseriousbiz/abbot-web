using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Services;

/// <summary>
/// Dispatches messages to a target platform.
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>
    /// Dispatches a message from a skill to Slack.
    /// </summary>
    /// <param name="message">The <see cref="BotMessageRequest"/> to dispatch.</param>
    /// <param name="organization">The organization that owns the Slack we're dispatching to.</param>
    Task<ProactiveBotMessageResponse> DispatchAsync(BotMessageRequest message, Organization organization);
}
