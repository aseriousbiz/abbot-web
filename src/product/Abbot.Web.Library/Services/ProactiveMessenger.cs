using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Logging;

namespace Serious.Abbot.Messages;

/// <summary>
/// Client used to send proactive messages to the underlying chat platform.
/// </summary>
/// <remarks>
/// This client is used by the ReplyController in abbot-web. The ReplyController provides an API to skills to call
/// in order to reply to chat asynchronously.
/// </remarks>
public class ProactiveMessenger : IProactiveMessenger
{
    static readonly ILogger<ProactiveMessenger> Log = ApplicationLoggerFactory.CreateLogger<ProactiveMessenger>();

    readonly ISkillRepository _skillRepository;
    readonly IMessageDispatcher _messageDispatcher;

    /// <summary>
    /// Constructs an instance of <see cref="ProactiveMessenger"/>.
    /// </summary>
    /// <remarks>
    /// Since messages can be sent with a background service such as Hangfire, we need to make sure we don't inject
    /// a DbContext here. This is why we use an <see cref="IServiceProvider"/> here.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if the App Setting MicrosoftAppPassword is not set.</exception>
    public ProactiveMessenger(ISkillRepository skillRepository, IMessageDispatcher messageDispatcher)
    {
        _skillRepository = skillRepository;
        _messageDispatcher = messageDispatcher;
    }

    [Obsolete("Use other overload")]
    public async Task<ProactiveBotMessageResponse?> SendMessageAsync(ProactiveBotMessage message)
    {
        var skillId = (Id<Skill>)message.SkillId;
        var messageRequest = message.TranslateToRequest();
        return await SendMessageAsync(skillId, messageRequest);
    }

    public async Task<ProactiveBotMessageResponse?> SendMessageAsync(Id<Skill> skillId, BotMessageRequest messageRequest)
    {
        var skill = await _skillRepository.GetByIdAsync(skillId);
        if (skill is null)
        {
            // If a skill is deleted with messages enqueued, we shouldn't continue to send it's messages.
            Log.EntityNotFound(skillId);
            return null;
        }

        return await SendMessageFromSkillAsync(skill, messageRequest);
    }

    public async Task<ProactiveBotMessageResponse> SendMessageFromSkillAsync(Skill skill, BotMessageRequest messageRequest)
    {
        var organization = skill.Organization;

        var response = await _messageDispatcher.DispatchAsync(messageRequest, organization);
        return response;
    }
}
