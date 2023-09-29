using Hangfire;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Provides an endpoint for Skills to reply back to chat asynchronously.
/// </summary>
public class ReplyController : SkillRunnerApiControllerBase
{
    readonly IProactiveMessenger _proactiveMessenger;
    readonly IBackgroundJobClient _backgroundJobClient;

    public ReplyController(IProactiveMessenger proactiveMessenger, IBackgroundJobClient backgroundJobClient)
    {
        _proactiveMessenger = proactiveMessenger;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// The endpoint for replying to chat. Skills send a <see cref="ProactiveBotMessage" /> which supplies information
    /// about the message to send.
    /// </summary>
    /// <param name="botMessage">The message to send.</param>
    [HttpPost("reply")]
    public async Task<IActionResult> PostAsync([FromBody] ProactiveBotMessage botMessage)
    {
        var messageRequest = botMessage.TranslateToRequest();

        if (botMessage.Schedule is 0)
        {
            // TODO: Use Skill from SkillRunnerApiControllerBase
            var response = await _proactiveMessenger.SendMessageFromSkillAsync(Skill, messageRequest);

            if (response is { Message: null or "" })
            {
                response = response with
                {
                    Message = response.Success
                        ? "Message sent immediately."
                        : "Message not sent.",
                };
            }

            return response.Success
                ? Ok(response)
                : StatusCode(500, response);
        }
        else
        {
            var delay = TimeSpan.FromSeconds(botMessage.Schedule);
            Id<Skill> skillId = Skill;
            _backgroundJobClient.Schedule<IProactiveMessenger>(
                messenger => messenger.SendMessageAsync(skillId, messageRequest),
                delay);

            return Ok(new ProactiveBotMessageResponse(
                true,
                $"Message scheduled to be sent in {delay.Humanize()}."));
        }
    }
}
