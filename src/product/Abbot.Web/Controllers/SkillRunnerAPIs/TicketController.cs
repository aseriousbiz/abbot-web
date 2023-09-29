using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Functions.Models;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers;

public class TicketController : SkillRunnerApiControllerBase
{
    readonly OpenTicketMessageBuilder _openTicketMessageBuilder;
    readonly IConversationRepository _conversationRepository;
    readonly IProactiveMessenger _proactiveMessenger;

    public TicketController(
        OpenTicketMessageBuilder openTicketMessageBuilder,
        IConversationRepository conversationRepository,
        IProactiveMessenger proactiveMessenger)
    {
        _openTicketMessageBuilder = openTicketMessageBuilder;
        _conversationRepository = conversationRepository;
        _proactiveMessenger = proactiveMessenger;
    }

    [HttpPost("ticket/buttons")]
    public async Task<IActionResult> PostAsync([FromBody] TicketPromptRequest request)
    {
        var (channel, threadId, conversationId) = request.ConversationIdentifier;

        var conversation = conversationId.HasValue
            ? await _conversationRepository.GetConversationAsync(conversationId.Value)
            : null;

        // A little extra protection here.
        if (conversation is not null && conversation.OrganizationId != Skill.OrganizationId)
        {
            return NotFound();
        }

        var messageId = request.MessageId;
        threadId = conversation?.FirstMessageId ?? threadId ?? messageId;

        var blocks = await _openTicketMessageBuilder.BuildOpenTicketMessageBlocksAsync(
            conversation,
            channel,
            threadId,
            Skill.Organization);

        channel = conversation?.Room.PlatformRoomId ?? channel;

        IRoomMessageTarget messageTarget = new RoomMessageTarget(channel.Require());

        var chatAddress = threadId is not null && threadId != messageId
            ? messageTarget.GetThread(threadId).Address
            : messageTarget.Address;

        var message = new BotMessageRequest(
            "Please select an action.",
            chatAddress with { EphemeralUser = request.User },
            Blocks: blocks
        );
        var response = await _proactiveMessenger.SendMessageAsync(Skill, message).Require();

        return response.Success
            ? Ok(new ApiResult())
            : StatusCode(500, new ApiResult(response.Message));
    }
}
