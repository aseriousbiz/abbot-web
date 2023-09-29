using Hangfire;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Messages;

/// <summary>
/// Client used to send proactive messages to the underlying chat platform.
/// </summary>
/// <remarks>
/// This client is used by the ReplyController in abbot-web. The ReplyController provides an API to skills to call
/// in order to reply to chat asynchronously.
/// </remarks>
public interface IProactiveMessenger
{
    /// <summary>
    /// Dispatches the message to its final destination.
    /// </summary>
    /// <param name="skillId">The <see cref="Skill"/> Id.</param>
    /// <param name="messageRequest">The message to send.</param>
    [Queue(HangfireQueueNames.HighPriority)]
    Task<ProactiveBotMessageResponse?> SendMessageAsync(Id<Skill> skillId, BotMessageRequest messageRequest);

    /// <summary>
    /// Dispatches the message to its final destination.
    /// </summary>
    /// <param name="skill">The <see cref="Skill"/>.</param>
    /// <param name="messageRequest">The message to send.</param>
    Task<ProactiveBotMessageResponse> SendMessageFromSkillAsync(Skill skill, BotMessageRequest messageRequest);
}
